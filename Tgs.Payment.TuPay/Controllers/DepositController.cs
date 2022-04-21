using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using Tgs.Payment.Clients;
using Tgs.Payment.TuPay.ApplicationServices;
using Tgs.ServiceUtility;

namespace Tgs.Payment.TuPay.Controllers;

[Route("[controller]")]
[ApiController]
public class DepositController : ControllerBase
{
	private readonly ILogger<DepositController> _logger;

	public DepositController(ILogger<DepositController> logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	[Consumes("application/x-www-form-urlencoded")]
	[Produces(MediaTypeNames.Text.Plain)]
	[HttpPost("callback", Name = "DepositCallback")]
	public async Task<string> DepositCallbackAsync(
		[FromServices] TuPayHasher hasher,
		[FromServices] IGetService<GetDepositFormQuery, DepositForm?> getFormService,
		[FromServices] IPaymentServiceClient paymentServiceClient,
		[FromServices] TuPaySerializer tuPaySerializer,
		[FromServices] IPaymentClient paymentClient,
		[FromForm] IFormCollection formData)
	{
		var unspecifyPayDate = DateTime.ParseExact(formData["endtime"], "yyyy-MM-dd HH:mm:ss", null);
		var specifyPayDate = DateTime.SpecifyKind(
			new DateTimeOffset(unspecifyPayDate, TimeSpan.FromHours(7)).UtcDateTime,
			DateTimeKind.Utc);

		var paymentReturn = new TuPayDepositReturn(
			formData["tradeno"],
			Guid.Parse(formData["outtradeno"]),
			decimal.Parse(formData["amount"]),
			decimal.Parse(formData["ramount"]),
			string.IsNullOrEmpty(formData["oamount"]) ? null : decimal.Parse(formData["oamount"]),
			specifyPayDate,
			formData["status"],
			formData["remark"]);

		_logger.LogInformation("PaymentReturn: {paymentReturn}", paymentReturn);

		if (paymentReturn.Status is not "1")
			return "success";

		var depositForm = await getFormService
			.GetAsync(new GetDepositFormQuery(paymentReturn.OutTradeNo))
			.ConfigureAwait(false);

		if (depositForm is null)
			return "fail";

		var hash = hasher.ComputeHash(
			depositForm.ApplyInfo!.Settings.Secret,
			formData
				.Where(kv => kv.Key != "sign")
				.Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value)));

		if (hash != formData["sign"].ToString())
			return "fail";

		if (depositForm.Status < PaymentFlowState.WaitApprove)
		{
			await paymentServiceClient.SubmitDepositFormStatusAsync(
				depositForm.Id,
				paymentReturn: tuPaySerializer.Serialize(paymentReturn)).ConfigureAwait(false);

			await paymentClient.ApproveDepositAsync(
				depositForm.Id,
				depositForm.BrandId,
				null).ConfigureAwait(false);
		}

		return "success";
	}
}
