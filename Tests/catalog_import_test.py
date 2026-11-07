"""
Catalogue acceptance test (constitution V), against a running backend and its MySQL database. Self-contained (no
files needed): two test companies are loaded side by side with the same product names but other prices and
quantities, through the API a person uses:
  - inventory products (Products API) -> categorization file -> package-items file -> names mapped
  - re-importing duplicates nothing
  - each company sees and writes only its own data; the database refuses a cross-company reference
  - package totals = quantity x inventory price, by hand = CATL_VIEW_PKQT
  - SYST_PROC_DLCO removes both companies and nothing of theirs is left
The import rules case by case are unit tests (Tests/UnitTests/CATL).

  python -I Tests/catalog_import_test.py --api http://localhost:5195/api --db "Server=localhost;Port=3306;Database=...;User=...;Password=..."
Needs: pymysql.
"""
from __future__ import annotations

import argparse, json, sys, time, uuid, urllib.error, urllib.request
from decimal import Decimal
import pymysql

failures: list[str] = []
def ok(cond: bool, message: str) -> None:
    print(("PASS  " if cond else "FAIL  ") + message)
    if not cond: failures.append(message)

# product name, colour, price, family
INVENTORY = [("Piano Switch 2 Gang", "White", 17311), ("Piano Switch 2 Gang", "Black", 17500), ("Gas Sensor", None, 18684),
             ("Smart Panel Q2", None, 78300), ("Curtain Motor", None, 33495)]
CATEGORIZATION = [["Product Name", "Standard Solution", "Premium Solution"],
                  ["Lighting Control", None, None], ["Piano Series", "✓", "✓"], ["Touch Panels", None, "✓"],
                  ["Safety", None, None], ["Gas Sensors", "✓", None], ["Curtains", "✓", "✓"]]
FAMILY = {"Piano Switch 2 Gang": "Piano Series", "Gas Sensor": "Gas Sensors", "Smart Panel Q2": "Touch Panels", "Curtain Motor": "Curtains"}
PACKAGES = [["Product", "Unit Price", "5 Marla\nAutomation", "Price", "10 Marla", "1 Kanal"],
            ["Piano Switch 2 Gang", "1", "6", "0", "12", "20"], ["Gas Sensor ", "1", "1", "0", "1", "2"],
            ["Smart Panel Q2", "1", "1", "0", "2", "2"], ["Curtian Motor", "1", "", "0", "2", "4"]]   # a typo: mapped by a person


class Api:
    def __init__(self, base: str): self.base, self.token = base, None
    def call(self, method: str, path: str, body=None, form=None, expect_error: bool = False):
        headers = {"Authorization": f"Bearer {self.token}"} if self.token else {}
        data = None
        if form is not None:
            boundary = uuid.uuid4().hex
            data = ("".join(f'--{boundary}\r\nContent-Disposition: form-data; name="{k}"\r\n\r\n{v}\r\n' for k, v in form.items()) + f"--{boundary}--\r\n").encode()
            headers["Content-Type"] = f"multipart/form-data; boundary={boundary}"
        elif body is not None:
            data = json.dumps(body).encode(); headers["Content-Type"] = "application/json"
        req = urllib.request.Request(self.base + path, data=data, method=method, headers=headers)
        try:
            with urllib.request.urlopen(req, timeout=300) as r:
                text, status = r.read().decode(), r.status
        except urllib.error.HTTPError as e:
            text, status = e.read().decode(), e.code
            if not expect_error: raise AssertionError(f"{method} {path} -> {status}: {text[:300]}")
        return status, (json.loads(text) if text and text[0] in "[{" else text)
    def company(self, name: str, email: str) -> "Api":
        self.call("POST", "/auth/register", {"fullName": name, "email": email, "password": "Test-12345"})
        self.token = self.call("POST", "/auth/login", {"email": email, "password": "Test-12345"})[1]["token"]
        return self


def load(api: Api, price_factor: Decimal, qty_factor: int) -> dict:
    for name, color, price in INVENTORY:
        full = f"{name} {color}" if color else name
        api.call("POST", "/products", form={"productName": full, "sku": f"T-{uuid.uuid4().hex[:8]}", "price": str(Decimal(price) * price_factor),
                                            "quantity": 0, "sellingType": "pos", "productType": "single", "taxesJson": "[]", "color": color or ""})
    r = api.call("POST", "/catalog/import/categorization", {"fileName": "cat.xlsx", "rows": CATEGORIZATION})[1]
    ok(r["familiesCreated"] == 4, f"categorization: {r['categories']} categories, {r['familiesCreated']} families created")
    cat = api.call("GET", "/catalog")[1]
    ok(len(cat["products"]) == 4 and sum(len(p["skus"]) for p in cat["products"]) == 5, "inventory: 5 products -> 4 catalogue products (one in two colours)")
    families = {f["name"]: f["id"] for g in cat["groups"] for f in g["families"]}
    rows = [PACKAGES[0]] + [[r[0], r[1]] + [str(int(c) * qty_factor) if c else c for c in r[2:]] for r in PACKAGES[1:]]
    r = api.call("POST", "/catalog/import/packages", {"fileName": "items.xlsx", "rows": rows})[1]
    ok(r["newPackageSizes"] == ["5 Marla", "10 Marla", "1 Kanal"], f"package items: sizes {r['newPackageSizes']} (price columns skipped)")
    # what a person does on the Import tab: a family for each product found, the typo mapped to its product
    cat = api.call("GET", "/catalog")[1]
    by_name = {p["name"]: p["id"] for p in cat["products"]}
    for u in cat["unmatched"]:
        held = json.loads(u["rowJson"])
        product = held.get("productId") or by_name["Curtain Motor"]
        name = next(n for n, i in by_name.items() if i == product)
        api.call("POST", f"/catalog/unmatched/{u['id']}/map", {"catalogProductId": product, "newFamilyId": families[FAMILY[name]], "create": False})
    cat = api.call("GET", "/catalog")[1]
    ok(not cat["unmatched"], "every package item mapped")
    return cat


def hand_total(cat: dict, size: str, tier_code: str) -> Decimal:
    size_id = next(s["id"] for s in cat["sizes"] if s["name"] == size)
    tier_id = next(t["id"] for t in cat["tiers"] if t["code"] == tier_code)
    products = {p["id"]: p for p in cat["products"]}
    total = Decimal(0)
    for l in cat["packageLines"]:
        if l["sizeId"] == size_id and l["tierId"] == tier_id:
            p = products[l["productId"]]
            sku = next((s for s in p["skus"] if s["isDefault"]), None)
            total += (Decimal(str(l["quantity"])) * Decimal(str(sku["price"] if sku else p["fallbackPrice"] or 0))).quantize(Decimal("0.01"))
    return total


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--api", default="http://localhost:5195/api")
    ap.add_argument("--db", required=True, help=".NET style connection string of the backend's database")
    args = ap.parse_args()
    parts = {k.strip().lower(): v.strip() for k, _, v in (p.partition("=") for p in args.db.split(";") if "=" in p)}
    db = pymysql.connect(host=parts.get("server", "localhost"), port=int(parts.get("port", 3306)), user=parts["user"], password=parts["password"],
                         database=parts["database"], autocommit=True)
    stamp = int(time.time())
    a = Api(args.api).company("Catalog Test A", f"catalog-a-{stamp}@test.local")
    b = Api(args.api).company("Catalog Test B", f"catalog-b-{stamp}@test.local")
    with db.cursor() as cur:
        cur.execute("SELECT TenantId FROM PRTY_MAST WHERE Email IN (%s, %s) ORDER BY Email", (f"catalog-a-{stamp}@test.local", f"catalog-b-{stamp}@test.local"))
        tenants = [r[0] for r in cur.fetchall()]
    try:
        print("-- company A --")
        ca = load(a, Decimal(1), 1)
        snapshot = lambda c: (len(c["products"]), sum(len(p["skus"]) for p in c["products"]), len(c["sizes"]), len(c["packageLines"]), sum(len(g["families"]) for g in c["groups"]))
        first = snapshot(ca)
        a.call("POST", "/catalog/import/categorization", {"fileName": "cat.xlsx", "rows": CATEGORIZATION})
        a.call("POST", "/catalog/import/packages", {"fileName": "items.xlsx", "rows": PACKAGES})
        ok(snapshot(a.call("GET", "/catalog")[1]) == first, f"re-importing duplicates nothing {first}")

        print("-- company B: same names, prices + 10 %, quantities x 2 --")
        cb = load(b, Decimal("1.1"), 2)
        ga = next(p for p in ca["products"] if p["name"] == "Gas Sensor")
        gb = next(p for p in cb["products"] if p["name"] == "Gas Sensor")
        ok(ga["id"] != gb["id"] and abs(gb["skus"][0]["price"] - ga["skus"][0]["price"] * 1.1) < 0.02, "same product name, own row and price per company")
        ok(not {p["id"] for p in ca["products"]} & {p["id"] for p in cb["products"]}, "no product of one company is listed for the other")
        status, _ = a.call("PUT", "/catalog/package-lines", [{"sizeId": cb["sizes"][0]["id"], "tierId": cb["tiers"][0]["id"], "productId": gb["id"], "quantity": 5}], expect_error=True)
        ok(status == 400, f"A can't write B's package lines ({status})")
        status, _ = a.call("PUT", f"/catalog/products/{gb['id']}", {"name": "hijack"}, expect_error=True)
        ok(status == 404, f"A can't edit B's product ({status})")
        with db.cursor() as cur:
            try:
                cur.execute("INSERT INTO CATL_PKLN (TenantId, PackageSizeId, TierId, CatalogProductId, Quantity) VALUES (%s, %s, %s, %s, 1)",
                            (tenants[0], ca["sizes"][0]["id"], ca["tiers"][0]["id"], gb["id"]))
                ok(False, "the database refuses a line pointing at another company's product")
            except pymysql.err.IntegrityError:
                ok(True, "the database refuses a line pointing at another company's product (composite foreign key)")

        print("-- package totals: quantity x inventory price --")
        ca = a.call("GET", "/catalog")[1]
        for size in ("10 Marla", "1 Kanal"):
            for tier in ("standard", "premium"):
                hand = hand_total(ca, size, tier)
                with db.cursor() as cur:
                    cur.execute("SELECT COALESCE(SUM(LineTotal), 0) FROM CATL_VIEW_PKQT q JOIN CATL_TIER t ON t.Id = q.TierId "
                                "WHERE q.TenantId = %s AND q.PackageName = %s AND t.Code = %s", (tenants[0], size, tier))
                    view = Decimal(str(cur.fetchone()[0]))
                ok(hand == view and hand > 0, f"A {size} {tier}: by hand {hand} = CATL_VIEW_PKQT {view}")
            ok(hand_total(cb, size, "standard") > hand_total(ca, size, "standard"), f"B {size} standard is its own (dearer, double)")
    finally:
        with db.cursor() as cur:
            for t in tenants:
                cur.execute("CALL SYST_PROC_DLCO(%s)", (t,))
            left = 0
            for table in ("SYST_TNNT", "PRTY_MAST", "CATL_PROD", "INVT_PROD", "CATL_PKLN", "CATL_FMLY", "CATL_SIZE", "ACCT_JRNL"):
                col = "Id" if table == "SYST_TNNT" else "TenantId"
                cur.execute(f"SELECT COUNT(*) FROM {table} WHERE {col} IN ({','.join(['%s'] * len(tenants))})", tenants)
                left += cur.fetchone()[0]
            ok(left == 0, f"SYST_PROC_DLCO removed both test companies ({left} rows left)")
    print(f"\n{'ALL PASSED' if not failures else f'{len(failures)} FAILED'}")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
