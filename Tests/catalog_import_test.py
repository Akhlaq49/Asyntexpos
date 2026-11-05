"""
Catalogue acceptance test, against a running backend and its database:
  - loads a company's three files (price list, categorization, packages) through the API, maps the names a person
    would map (fixtures/smartinn_mapping.json), re-imports everything and proves nothing is duplicated
  - loads a second company side by side with the same product names but other prices and quantities, and proves each
    company sees only its own data (and can't write the other's)
  - package totals = quantity x active price, worked out by hand, for two package sizes
  - removes both companies with sp_delete_company and checks nothing of theirs is left

Run (backend on API, database reachable with DB):
  python -I Tests/catalog_import_test.py --api http://localhost:5195/api --files D:/Work/Asyntex \
      --db "Server=localhost;Port=3306;Database=SmartinnPosDb;User=...;Password=..."
Needs: openpyxl, pymysql.
"""
from __future__ import annotations

import argparse
import json
import sys
import time
import urllib.error
import urllib.request
from decimal import Decimal
from pathlib import Path

import openpyxl
import pymysql

HERE = Path(__file__).resolve().parent
FILES = {"pricelist": "Pak Price List.xlsx", "categorization": "Smartinn Product categorization.xlsx", "packages": "packages.xlsx"}
failures: list[str] = []


def ok(cond: bool, message: str) -> None:
    print(("PASS  " if cond else "FAIL  ") + message)
    if not cond:
        failures.append(message)


def key(s: str | None) -> str:
    return " ".join((s or "").lower().split())


class Api:
    def __init__(self, base: str):
        self.base, self.token = base.rstrip("/"), None

    def call(self, method: str, path: str, body=None, expect_error: bool = False):
        req = urllib.request.Request(self.base + path, method=method, data=None if body is None else json.dumps(body).encode(),
                                     headers={"Content-Type": "application/json", **({"Authorization": "Bearer " + self.token} if self.token else {})})
        try:
            with urllib.request.urlopen(req, timeout=300) as res:
                raw = res.read()
                return res.status, (json.loads(raw) if raw else None)
        except urllib.error.HTTPError as e:
            text = e.read().decode(errors="replace")
            if not expect_error:
                raise AssertionError(f"{method} {path} -> {e.code}: {text[:400]}")
            return e.code, text

    def company(self, name: str, email: str) -> "Api":
        self.call("POST", "/auth/register", {"fullName": name, "email": email, "password": "Test-12345"})
        self.token = self.call("POST", "/auth/login", {"email": email, "password": "Test-12345"})[1]["token"]
        return self


def rows_of(path: Path, change=None) -> list[list[str | None]]:
    """The sheet as the browser sends it: text cells; change(row) may alter a row (other company's prices / quantities)"""
    ws = openpyxl.load_workbook(path, data_only=True).active
    rows = [[None if c is None else str(c) for c in r] for r in ws.iter_rows(values_only=True)]
    return [change(r) if change else r for r in rows]


def import_file(api: Api, kind: str, files: Path, change=None) -> dict:
    return api.call("POST", f"/catalog/import/{kind}", {"fileName": FILES[kind], "rows": rows_of(files / FILES[kind], change)})[1]


def load_company(api: Api, files: Path, mapping: dict, price_change=None, qty_change=None) -> None:
    """A company's first load: the three files, then the names a person maps, then families for products that matched by name"""
    r = import_file(api, "pricelist", files, price_change)
    ok(r["rows"] == r["newSkus"] + r["updatedSkus"] and r["newSkus"] >= r["rows"] - 2,
       f"price list: {r['rows']} rows -> {r['newSkus']} SKUs ({r['updatedSkus']} repeated rows), {r['newProducts']} products")
    ok({d["model"] for d in r["duplicateModelCodes"]} >= {"T1L3G", "STGW"}, "duplicate model codes flagged, not fixed: " + ", ".join(d["model"] for d in r["duplicateModelCodes"]))
    r = import_file(api, "categorization", files)
    ok(r["families"] == 24 and r["familiesCreated"] == 24, f"categorization: {r['categories']} categories, {r['families']} families")
    r = import_file(api, "packages", files, qty_change)
    ok(len(r["newPackageSizes"]) == 8 and r["unmatched"] > 0, f"packages: sizes {r['newPackageSizes']}, {r['matched']} matched, {r['unmatched']} waiting to be mapped")

    cat = api.call("GET", "/catalog")[1]
    products = {key(p["name"]): p for p in cat["products"]}
    families = {key(f["name"]): f for g in cat["groups"] for f in g["families"]}
    wanted = {key(k): v for k, v in mapping["packages"].items()}
    for u in [u for u in cat["unmatched"] if u["kind"] == "packages"]:
        m = wanted[key(u["name"])]
        family = families[key(m["family"])]["id"] if m["family"] else None
        if m["product"]:
            api.call("POST", f"/catalog/unmatched/{u['id']}/map", {"catalogProductId": products[key(m["product"])]["id"], "newFamilyId": family, "create": False})
        else:
            api.call("POST", f"/catalog/unmatched/{u['id']}/map", {"create": True, "newFamilyId": family})
    cat = api.call("GET", "/catalog")[1]
    ok(not [u for u in cat["unmatched"] if u["kind"] == "packages"], "every packages name mapped (saved as aliases)")
    # products that matched by their own name: their family is chosen on the Package Quantities tab
    products = {key(p["name"]): p for p in cat["products"]}
    for m in wanted.values():
        if m["product"] and m["family"]:
            p = products[key(m["product"])]
            if p["familyId"] is None:
                api.call("PUT", f"/catalog/products/{p['id']}", {"name": p["name"], "familyId": families[key(m["family"])]["id"], "unit": p["unit"], "fallbackPrice": p["fallbackPrice"]})
    r = import_file(api, "packages", files, qty_change)
    ok(r["unmatched"] == 0, f"packages again: {r['matched']} matched, {r['linesWritten']} lines, no family: {len(r['noFamily'])}")


def snapshot(api: Api) -> dict:
    cat = api.call("GET", "/catalog")[1]
    return {
        "products": len(cat["products"]),
        "skus": sum(len(p["skus"]) for p in cat["products"]),
        "aliases": sum(len(p["aliases"]) for p in cat["products"]),
        "families": sum(len(g["families"]) for g in cat["groups"]),
        "sizes": len(cat["sizes"]),
        "packageLines": len(cat["packageLines"]),
        "priceLists": len(cat["priceLists"]),
        "activePriceLists": sum(1 for l in cat["priceLists"] if l["isActive"]),
    }


def hand_total(cat: dict, size_name: str, tier_code: str) -> Decimal:
    """quantity x the default SKU's price (active price list); a product with no SKU uses its packages-file price"""
    size = next(s for s in cat["sizes"] if key(s["name"]) == key(size_name))
    tier = next(t for t in cat["tiers"] if t["code"] == tier_code)
    products = {p["id"]: p for p in cat["products"]}
    total = Decimal(0)
    for l in cat["packageLines"]:
        if l["sizeId"] != size["id"] or l["tierId"] != tier["id"]:
            continue
        p = products[l["productId"]]
        sku = next((s for s in p["skus"] if s["isDefault"]), None)
        price = Decimal(str(sku["price"])) if sku else Decimal(str(p["fallbackPrice"] or 0))
        total += (Decimal(str(l["quantity"])) * price).quantize(Decimal("0.01"))
    return total


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--api", default="http://localhost:5195/api")
    ap.add_argument("--files", default="D:/Work/Asyntex")
    ap.add_argument("--db", required=True, help=".NET style connection string of the backend's database")
    args = ap.parse_args()
    files = Path(args.files)
    mapping = json.loads((HERE / "fixtures" / "smartinn_mapping.json").read_text(encoding="utf8"))
    parts = {k.strip().lower(): v.strip() for k, _, v in (p.partition("=") for p in args.db.split(";") if "=" in p)}
    db = pymysql.connect(host=parts.get("server", "localhost"), port=int(parts.get("port", 3306)), user=parts.get("user"),
                         password=parts.get("password"), database=parts.get("database"), autocommit=True)
    stamp = int(time.time())
    a = Api(args.api).company("Catalog Test A", f"catalog-a-{stamp}@test.local")
    b = Api(args.api).company("Catalog Test B", f"catalog-b-{stamp}@test.local")
    tenants = []
    try:
        with db.cursor() as cur:
            cur.execute("SELECT TenantId FROM Parties WHERE Email IN (%s, %s) ORDER BY Email", (f"catalog-a-{stamp}@test.local", f"catalog-b-{stamp}@test.local"))
            tenants = [r[0] for r in cur.fetchall()]

        print("-- company A: first load --")
        load_company(a, files, mapping)
        first = snapshot(a)
        print("   ", first)
        print("-- company A: the three files again --")
        for kind in ("pricelist", "categorization", "packages"):
            import_file(a, kind, files)
        again = snapshot(a)
        ok(again == first, f"re-import duplicates nothing: {again}")
        ok(again["activePriceLists"] == 1, "exactly one active price list")

        print("-- company B: same names, other prices and quantities --")
        def dearer(row):      # price list: End User Price + 10 %
            if row and len(row) > 6 and row[6] not in (None, "") and row[0] not in (None, "SN"):
                try: row[6] = str(round(float(row[6]) * 1.1, 2))
                except ValueError: pass
            return row
        def double(row):      # packages: every quantity x 2
            if row and row[0] and row[0].strip() != "Product":
                for i in range(2, len(row), 2):
                    try: row[i] = None if row[i] in (None, "") else str(float(row[i]) * 2)
                    except ValueError: pass
            return row
        load_company(b, files, mapping, dearer, double)

        ca, cb = a.call("GET", "/catalog")[1], b.call("GET", "/catalog")[1]
        echo_a = next(p for p in ca["products"] if p["name"] == "Harmony HPAD Echo")
        echo_b = next(p for p in cb["products"] if p["name"] == "Harmony HPAD Echo")
        ok(echo_a["id"] != echo_b["id"] and abs(echo_b["skus"][0]["price"] - echo_a["skus"][0]["price"] * 1.1) < 0.02,
           f"same product name, own row and price per company: A {echo_a['skus'][0]['price']}, B {echo_b['skus'][0]['price']}")
        ids_a, ids_b = {p["id"] for p in ca["products"]}, {p["id"] for p in cb["products"]}
        ok(not ids_a & ids_b, "no product of one company is listed for the other")
        status, _ = a.call("PUT", "/catalog/package-lines", [{"sizeId": cb["sizes"][0]["id"], "tierId": cb["tiers"][0]["id"], "productId": echo_b["id"], "quantity": 5}], expect_error=True)
        ok(status == 400, f"A can't write B's package lines ({status})")
        status, _ = a.call("PUT", f"/catalog/products/{echo_b['id']}", {"name": "hijack"}, expect_error=True)
        ok(status == 404, f"A can't edit B's product ({status})")
        with db.cursor() as cur:
            try:
                cur.execute("INSERT INTO CatalogPackageLines (TenantId, PackageSizeId, TierId, CatalogProductId, Quantity) VALUES (%s, %s, %s, %s, 1)",
                            (tenants[0], ca["sizes"][0]["id"], ca["tiers"][0]["id"], echo_b["id"]))
                ok(False, "database refuses a line pointing at another company's product")
            except pymysql.err.IntegrityError:
                ok(True, "database refuses a line pointing at another company's product (composite foreign key)")

        print("-- package totals: quantity x active price --")
        for size in ("10 Marla", "1 kanal"):
            for tier in ("standard", "premium"):
                hand = hand_total(ca, size, tier)
                with db.cursor() as cur:
                    cur.execute("SELECT COALESCE(SUM(LineTotal), 0) FROM v_package_quote q JOIN CatalogTiers t ON t.Id = q.TierId "
                                "WHERE q.TenantId = %s AND q.PackageName = %s AND t.Code = %s", (tenants[0], size, tier))
                    view = Decimal(str(cur.fetchone()[0]))
                ok(hand == view and hand > 0, f"A {size} {tier}: by hand {hand} = v_package_quote {view}")
            hb = hand_total(cb, size, "standard")
            ok(hb > hand_total(ca, size, "standard"), f"B {size} standard {hb} (dearer, double quantities) - its own figures")
    finally:
        with db.cursor() as cur:
            for t in tenants:
                cur.execute("CALL sp_delete_company(%s)", (t,))
            left = 0
            for table in ("Tenants", "Parties", "CatalogProducts", "Products", "CatalogPackageLines", "SolutionProductLines", "PackageSizes", "CatalogPriceLists"):
                col = "Id" if table == "Tenants" else "TenantId"
                if tenants:
                    cur.execute(f"SELECT COUNT(*) FROM {table} WHERE {col} IN ({','.join(['%s'] * len(tenants))})", tenants)
                    left += cur.fetchone()[0]
            ok(left == 0, f"sp_delete_company removed both test companies ({left} rows left)")
    print(f"\n{'ALL PASSED' if not failures else f'{len(failures)} FAILED'}")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
