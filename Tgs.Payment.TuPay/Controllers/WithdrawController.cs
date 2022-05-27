using Microsoft.AspNetCore.Mvc;
using Tgs.Payment.Clients;
using Tgs.Payment.TuPay.ApplicationServices;
using Tgs.ServiceUtility;

namespace Tgs.Payment.TuPay.Controllers;

[Route("[controller]")]
[ApiController]
public class WithdrawController : ControllerBase
{
	private readonly ILogger<WithdrawController> _logger;

	public WithdrawController(ILogger<WithdrawController> logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	[HttpPost("callback", Name = "WithdrawCallback")]
	public async Task<string> WithdrawCallbackAsync(
		[FromServices] TuPayHasher hasher,
		[FromServices] IGetService<GetWithdrawFormQuery, WithdrawForm?> getFormService,
		[FromServices] IPaymentServiceClient paymentServiceClient,
		[FromServices] IPaymentClient paymentClient,
		[FromServices] TuPaySerializer serializer,
		[FromForm] IFormCollection formData)
	{
		static DateTime? ConvertToSystemDate(string date)
		{
			if (string.IsNullOrWhiteSpace(date))
				return null;

			var unspecifyDate = DateTime.ParseExact(date, "yyyy-MM-dd HH:mm:ss", null);
			var specifyDate = DateTime.SpecifyKind(
				new DateTimeOffset(unspecifyDate, TimeSpan.FromHours(7)).UtcDateTime,
				DateTimeKind.Utc);

			return specifyDate;
		}

		var paymentReturn = new TuPayWithdrawReturn(
			formData["transferno"],
			Guid.Parse(formData["outtransferno"]),
			decimal.Parse(formData["tradeamount"]),
			decimal.Parse(formData["transferamount"]),
			ConvertToSystemDate(formData["endtime"]),
			formData["remark"],
			formData["status"]);

		_logger.LogInformation("PaymentReturn: {paymentReturn}", paymentReturn);

		if (paymentReturn.Status is not "1")
			return "success";

		var withdrawForm = await getFormService
			.GetAsync(new GetWithdrawFormQuery(paymentReturn.FormId))
			.ConfigureAwait(false);

		if (withdrawForm == null)
			return "fail";

		var hash = hasher.ComputeHash(
			withdrawForm.ApplyInfo!.Settings.Secret,
			formData
				.Where(kv => kv.Key != "sign")
				.Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value)));

		if (hash != formData["sign"].ToString())
			return "fail";

		if (withdrawForm.Status == PaymentFlowState.Approved)
		{
			await paymentServiceClient.SubmitWithdrawFormStatusAsync(
				withdrawForm.Id,
				paymentReturn: serializer.Serialize(paymentReturn)).ConfigureAwait(false);

			await paymentClient
				.PaidWithdrawAsync(withdrawForm.Id, withdrawForm.BrandId, HttpContext.RequestAborted)
				.ConfigureAwait(false);
		}

		return "success";
	}
}
