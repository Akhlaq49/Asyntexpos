using System.ComponentModel.DataAnnotations;

namespace ReactPosApi.Models;

/// <summary>
/// The company's terms &amp; conditions (one row per tenant), printed on the last pages
/// of quotations and purchase orders. Plain text, one item per line:
/// "- " = bullet, "1: Payment" = numbered heading, a short line on its own = section heading.
/// </summary>
public class TermsConditions : ITenantEntity
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(150)]
    public string? UpdatedBy { get; set; }
}

public static class DefaultTermsConditions
{
    public const string Text = """
Notes
- Product color, type, model & quantity will be verified by the client before payment. No changes will be entertained once order is confirmed.
- All prices are exclusive of GST.
Terms & Conditions
1: Payment
- 100% Advance
- Payment Method: Cheque, Online Transfer, RTGS
- Client must obtain payment receipt confirmation from sale person duly issued by Finance Dte.
- Any payment to POC without confirmation will not be accepted.
- The client shall ensure 100% payment of product in advance on closing date. However, installation charges shall be payable at the time of delivery at the client's premises.
2: Quote Validity
Quote Validity is One month.
3: Delivery
- Delivery timeline starts after advance payment received from client, as per payment terms.
- Only computer generated terms & conditions will be considered.
- No return/exchange once the order is placed.
4: Installation
- Installation done by client's MEP team.
- There will be 03 complimentary visits included in the project scope, subject to scope value.
- After exhausting the complimentary visits, additional visits will be charged upto PKR 10,000 per visit.
5: Cancellation
- In case of cancellation of the Order by the Customer, an amount equivalent to thirty percent of the total project value shall be charged as cancellation charges.
- After 7 days, order will not be cancelled.
- Refund claim will be processed after 1 month.
- Any third-party items mentioned above are subject to market availability. In the event that an item is unavailable or obsolete, the company will refund the amount paid by the customer for the item.
6: Warranty Clause
- The warranty period is two (02) years starts from the date of delivery.
- NO Warranty is applicable on 3rd party Items (Gate Motor, Google Device, Alexa, etc).
- Under warranty period client will pay callout charges for team mobilization to rectify the problem.
- Brand warranty claims are subject to product availability at the factory. If an item has been discontinued or is no longer in stock, it will be replaced with the current or equivalent version.
The following shall not be covered in warranty scope / claim:
- For those beyond the change or warranty period.
- Without getting the permission of One Touch, change its inherent structure circuit privately or dismantle or modify without authorization.
- Product damage caused by accident, human abuse/misuse, such as output short circuit, high-voltage input from power sources (WAPDA, UPS generator, or solar systems), overload, high temperature, water inflow, mechanical damage, broken, serious oxidation or rust etc.
- No warranty of consumable items, like batteries of cell.
- Damage caused by transportation loading and unloading on the way to return for repair.
- Product failure or damages caused by force majeure such as earthquake, fire, flood, lighting, strikes, acts of war or terrorism, civil or military disturbances, delayed by Pakistan Customs, natural catastrophes or acts of God.
- Product failure or damage caused by any problem other than product design, technology, quality, manufacturing, and others.
- Accessories like batteries, cables, adapter are not included in the free repair scope.
- Only those malfunctioning items that do not perform accurate functions would be rectified first; if it cannot be repaired, the item shall be replaced.
- Those products that are burnt by low/high voltages, damaged, or broken would not be reflected in the warranty claim.
7: Force Majeure
- In no event shall the One Touch Automation (Pvt) Ltd be responsible or liable for any failure or delay in the performance hereunder arising out of or caused by, strikes, acts of war or terrorism, civil or military disturbances, delayed by Pakistan Customs, nuclear or natural catastrophes or acts of God.
Acknowledgment
I hereby acknowledge that I have read & understood the terms and conditions mentioned above and I agree to all the terms.
Customer Signature: ______________________
Date: ______________________
""";
}
