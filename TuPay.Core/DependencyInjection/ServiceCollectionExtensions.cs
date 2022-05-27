using System.Reflection;
using Microsoft.Extensions.Options;
using Tgs.Payment.TuPay;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddTuPay(
		this IServiceCollection services,
		Action<TuPayOptions, IServiceProvider> configure)
		=> services
			.AddOptions<TuPayOptions>()
			.Configure(configure)
			.Services
			.AddHttpClient<TuPayClient>()
			.ConfigureHttpClient((sp, httpClient) =>
			{
				var options = sp.GetRequiredService<IOptions<TuPayOptions>>().Value;

				httpClient.BaseAddress = options.PaymentApiUrl;
			})
			.Services
			.RegisterAllServiceUtilityTypes(Assembly.GetExecutingAssembly(), ServiceLifetime.Transient)
			.AddPaymentClient()
			.AddPaymentServiceClient((options, sp) =>
			{
				var skyPayOptions = sp.GetRequiredService<IOptions<TuPayOptions>>().Value;

				options.PaymentServiceBaseUrl = skyPayOptions.PaymentServiceBaseUrl;
			})
			.AddTransient<TuPayHasher>()
			.AddSingleton<TuPaySerializer>();
}
