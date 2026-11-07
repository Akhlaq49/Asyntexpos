"""
Ledger acceptance test (double-entry posting through the normal API), in a fresh test company:
purchases, a sale with tax / shipping, payments both ways, an expense, a capital entry -> every entry balances,
the balance sheet balances, cost of goods sold is the moving average; a backdated purchase re-costs the sale;
deleting a document removes its entry; the lock date refuses changes; a rebuild gives the same ledger.
The company is deleted at the end (sp_delete_company).

    python -I Tests/ledger_test.py --api http://localhost:5195/api --db "Server=localhost;Port=3306;Database=...;User=...;Password=..."
"""
import argparse, json, sys, time, uuid, urllib.request, urllib.error
from datetime import date, timedelta
import pymysql

FAILED = []
def ok(cond, msg):
    print(("PASS  " if cond else "FAIL  ") + msg)
    if not cond: FAILED.append(msg)

class Api:
    def __init__(self, base): self.base, self.token = base, None
    def call(self, method, path, body=None, form=None, expect=None):
        headers = {"Authorization": f"Bearer {self.token}"} if self.token else {}
        data = None
        if form is not None:
            boundary = uuid.uuid4().hex
            parts = [f'--{boundary}\r\nContent-Disposition: form-data; name="{k}"\r\n\r\n{v}\r\n' for k, v in form.items()]
            data = ("".join(parts) + f"--{boundary}--\r\n").encode()
            headers["Content-Type"] = f"multipart/form-data; boundary={boundary}"
        elif body is not None:
            data = json.dumps(body).encode(); headers["Content-Type"] = "application/json"
        req = urllib.request.Request(self.base + path, data=data, method=method, headers=headers)
        try:
            with urllib.request.urlopen(req) as r:
                text = r.read().decode(); code = r.status
        except urllib.error.HTTPError as e:
            text = e.read().decode(); code = e.code
        if expect is None and code >= 400: raise AssertionError(f"{method} {path} -> {code}: {text[:300]}")
        try: return code, json.loads(text) if text else None
        except ValueError: return code, text

def main():
    ap = argparse.ArgumentParser(); ap.add_argument("--api", default="http://localhost:5195/api"); ap.add_argument("--db", required=True)
    args = ap.parse_args()
    parts = {k.strip().lower(): v.strip() for k, _, v in (p.partition("=") for p in args.db.split(";") if "=" in p)}
    db = pymysql.connect(host=parts.get("server", "localhost"), port=int(parts.get("port", 3306)), user=parts["user"], password=parts["password"], database=parts["database"], autocommit=True)
    api = Api(args.api)
    email = f"ledger-{int(time.time())}@test.local"
    api.call("POST", "/auth/register", {"fullName": "Ledger Test", "email": email, "password": "Test-12345"})
    api.token = api.call("POST", "/auth/login", {"email": email, "password": "Test-12345"})[1]["token"]
    with db.cursor() as c:
        c.execute("SELECT TenantId FROM Parties WHERE Email=%s", (email,)); tenant = c.fetchone()[0]
    today = date.today(); d = lambda n: (today - timedelta(days=n)).isoformat()
    try:
        prod = api.call("POST", "/products", form={"productName": "Ledger Switch", "sku": f"LT-{tenant}", "price": 250, "quantity": 0, "sellingType": "pos", "productType": "single", "taxesJson": "[]"})[1]
        pid = int(prod["id"])
        cat = api.call("POST", "/expense-categories", {"name": "Electricity", "description": "", "status": "active"})[1]

        def purchase(ref, qty, price, day, ship=0):
            return api.call("POST", "/purchases", {"supplierName": "Acme Supplies", "reference": ref, "date": day, "status": "Received", "orderTax": 0, "discount": 0,
                "shipping": ship, "total": qty * price + ship, "paid": 0,
                "items": [{"productId": pid, "productName": "Ledger Switch", "quantity": qty, "purchasePrice": price, "unitCost": price, "totalCost": qty * price}]})[1]
        p1 = purchase("LT-P1", 10, 100, d(10))
        api.call("POST", f"/purchases/{p1['id']}/payments", {"type": "Payment", "amount": 600, "method": "Bank Transfer", "date": d(9)})
        sale = api.call("POST", "/sales", {"customerName": "Ledger Customer", "grandTotal": 1070, "orderTax": 50, "discount": 0, "shipping": 20, "status": "Completed",
            "items": [{"productId": pid, "productName": "Ledger Switch", "quantity": 4, "purchasePrice": 250, "unitCost": 250, "totalCost": 1000}]})[1]
        api.call("POST", f"/sales/{sale['id']}/payments", {"reference": "LT-PAY", "receivedAmount": 500, "payingAmount": 500, "paymentType": "Cash"})
        p2 = purchase("LT-P2", 10, 130, d(-1))      # tomorrow: after the sale
        exp = api.call("POST", "/expenses", {"expenseName": "Bill", "expenseCategoryId": cat["id"], "description": "", "date": d(1), "amount": 75, "status": "active"})[1]
        api.call("POST", "/finance-entries", {"type": "OwnerInvestment", "name": "Owner", "amount": 5000, "method": "Cash", "date": d(20)}, expect=True)

        def acct(key_or_name):
            accounts = api.call("GET", "/accounting/accounts")[1]
            return next((a for a in accounts if a["systemKey"] == key_or_name or a["name"] == key_or_name), None)
        def bal(key): a = acct(key); return round(a["balance"], 2) if a else 0
        tb = api.call("GET", "/accounting/trial-balance")[1]
        ok(tb["balanced"], f"trial balance balances: {tb['totals']}")
        ok(api.call("GET", "/accounting/settings")[1]["unbalanced"] == [], "no unbalanced entry")
        ok(bal("cogs") == 400, f"cost of goods sold = 4 x average 100 = 400 (got {bal('cogs')})")
        ok(bal("stock") == 1000 - 400 + 1300, f"stock = 1000 - 400 + 1300 (got {bal('stock')})")
        ok(bal("receivable") == 570, f"customer owes 1070 - 500 (got {bal('receivable')})")
        ok(bal("payable") == 1000 - 600 + 1300, f"owed to supplier 1000 - 600 + 1300 (got {bal('payable')})")
        ok(bal("tax-payable") == 50 and bal("shipping-income") == 20 and bal("sales") == 1000, "sale split into sales 1000, tax 50, shipping 20")
        ok(bal("bank") == -600 and acct("bank")["code"] == "151002", f"bank transfer goes to 151002 Cash at Bank: -600 (got {bal('bank')})")
        ok(acct("cash")["code"] == "151001" and acct("receivable")["code"] == "153001" and acct("sales")["code"] == "511001" and acct("cogs")["code"] == "611001", "the company's account codes")
        ok(acct("Electricity")["code"].startswith("6610"), f"expense category account under 661000 General Administration: {acct('Electricity')['code']}")
        ok(bal("cash") == 500 - 75 + 5000, f"cash = 500 in - 75 expense + 5000 capital (got {bal('cash')})")
        ok(acct("Electricity") is not None and round(acct("Electricity")["balance"], 2) == 75, "expense category has its own account")
        pl = api.call("GET", f"/accounting/profit-and-loss?from={d(30)}&to={d(0)}")[1]
        ok(pl["netProfit"] == 1000 + 20 - 400 - 75, f"net profit 1000 + 20 - 400 - 75 = 545 (got {pl['netProfit']})")
        cash_pl = api.call("GET", f"/accounting/profit-and-loss?from={d(30)}&to={d(0)}&basis=cash")[1]
        share = 500 / 1070
        ok(abs(cash_pl["netProfit"] - round((1000 + 20 - 400) * share - 75, 2)) <= 0.05, f"cash basis: sale counted as paid ({share:.2%}): {cash_pl['netProfit']}")
        bs = api.call("GET", "/accounting/balance-sheet")[1]
        ok(bs["balanced"], f"balance sheet balances: assets {bs['totalAssets']} = liabilities + equity {bs['totalLiabilitiesAndEquity']}")

        # backdate the second purchase before the sale: the sale is re-costed at (1000 + 1300) / 20 = 115
        api.call("PUT", f"/purchases/{p2['id']}", {"supplierName": "Acme Supplies", "reference": "LT-P2", "date": d(10), "status": "Received", "orderTax": 0, "discount": 0, "shipping": 0,
            "total": 1300, "paid": 0, "items": [{"productId": pid, "productName": "Ledger Switch", "quantity": 10, "purchasePrice": 130, "unitCost": 130, "totalCost": 1300}]})
        ok(bal("cogs") == 460, f"backdated purchase re-costs the sale: 4 x 115 = 460 (got {bal('cogs')})")
        # delete the expense: its entry goes
        api.call("DELETE", f"/expenses/{exp['id']}")
        with db.cursor() as c:
            c.execute("SELECT COUNT(*) FROM JournalEntries WHERE TenantId=%s AND VoucherType='Expense'", (tenant,)); n = c.fetchone()[0]
        ok(n == 0 and bal("cash") == 5500, "deleted expense: its entry is gone, cash back to 5500")

        # lock date: nothing on or before it can change
        api.call("PUT", "/accounting/settings", {"lockDate": d(0)})
        code, res = api.call("POST", "/expenses", {"expenseName": "Late", "expenseCategoryId": cat["id"], "description": "", "date": d(2), "amount": 10, "status": "active"}, expect=True)
        ok(code == 400 and "locked" in json.dumps(res).lower(), f"expense dated in a locked period refused: {code} {str(res)[:120]}")
        with db.cursor() as c:
            c.execute("SELECT COUNT(*) FROM Expenses WHERE TenantId=%s AND ExpenseName='Late'", (tenant,)); n = c.fetchone()[0]
        ok(n == 0, "the refused expense was not saved either")
        code, res = api.call("DELETE", f"/sales/{sale['id']}", expect=True)
        ok(code >= 400, f"deleting a sale in a locked period refused: {code}")
        api.call("PUT", "/accounting/settings", {"lockDate": None})

        # rebuild gives the same ledger
        before = api.call("GET", "/accounting/trial-balance")[1]["rows"]
        api.call("POST", "/accounting/rebuild")
        after = api.call("GET", "/accounting/trial-balance")[1]["rows"]
        key = lambda rows: sorted((r["code"], round(r["closing"], 2)) for r in rows)
        ok(key(before) == key(after), "rebuild from the documents gives the same balances")
    finally:
        with db.cursor() as c: c.execute("CALL sp_delete_company(%s)", (tenant,))
        print("   cleanup: company", tenant, "deleted")
    print("\nALL PASSED" if not FAILED else f"\n{len(FAILED)} FAILED")
    return 1 if FAILED else 0

if __name__ == "__main__":
    sys.exit(main())
