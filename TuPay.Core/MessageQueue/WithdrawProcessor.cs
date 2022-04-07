using Google.Protobuf;
using Microsoft.Extensions.Options;
using Tgs.MessageQueue;
using Tgs.Payment.Clients;
using Tgs.Payment.Protos;

namespace Tgs.Payment.TuPay.MessageQueue;

internal class WithdrawProcessor : IMessageProcessor
{
	private readonly TuPaySerializer _tuPaySerializer;
	private readonly TuPayClient _tuPayClient;
	private readonly IPaymentClient _paymentClient;
	private readonly TuPayOptions _options;

	public WithdrawProcessor(
		TuPaySerializer tuPaySerializer,
		IOptions<TuPayOptions> tuPayOptionsAccessor,
		TuPayClient tuPayClient,
		IPaymentClient paymentClient)
	{
		_tuPaySerializer = tuPaySerializer ?? throw new ArgumentNullException(nameof(tuPaySerializer));
		_tuPayClient = tuPayClient ?? throw new ArgumentNullException(nameof(tuPayClient));
		_paymentClient = paymentClient ?? throw new ArgumentNullException(nameof(paymentClient));
		_options = tuPayOptionsAccessor.Value;
	}

	public async ValueTask<ReadOnlyMemory<byte>> HandleAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
	{
		var request = WithdrawRequest.Parser.ParseFrom(data.Span);
		var settings = _tuPaySerializer.Deserialize<TuPayWithdrawSettings>(request.ApplyInfo)!;

		var order = new TuPayClientWithdrawOrder(
			decimal.Parse(request.Amount),
			settings.BankName,
			settings.SubBankName,
			settings.CardNo,
			settings.CardName,
			new Uri($"{_options.CallbackBaseUrl}/withdraw/callback"),
			settings.FormId,
			settings.Remark);

		var remoteInfo = await _tuPayClient.WithdrawAsync(
			settings.Merchant,
			settings.Secret,
			order).ConfigureAwait(false);

		static DateTime ConvertToSystemDate(string date)
		{
			var unspecifyDate = DateTime.ParseExact(date, "yyyy-MM-dd HH:mm:ss", null);
			var specifyDate = DateTime.SpecifyKind(
				new DateTimeOffset(unspecifyDate, TimeSpan.FromHours(7)).UtcDateTime,
				DateTimeKind.Utc);

			return specifyDate;
		}

		var applyInfo = new TuPayWithdrawApplyInfo(
			settings,
			new TuPayWithdrawResponse(
				remoteInfo.TransferNo,
				remoteInfo.TradeAmount,
				remoteInfo.TransferAmount,
				string.IsNullOrWhiteSpace(remoteInfo.PayTime) ? null : ConvertToSystemDate(remoteInfo.PayTime),
				remoteInfo.Remark,
				remoteInfo.Status));

		var thirdPartyOrderNo = applyInfo.Response.TransferNo;
		await _paymentClient
			.SaveWithdrawFormThirdPartyOrderNoAsync(settings.FormId, thirdPartyOrderNo)
			.ConfigureAwait(false);

		var msg = new WithdrawResponse
		{
			Result = _tuPaySerializer.Serialize(applyInfo)
		};

		return msg.ToByteArray();
	}
}
