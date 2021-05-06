using ReactiveUI;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send
{
	[NavigationMetaData(Title = "Payment successful")]
	public partial class SendSuccessViewModel : RoutableViewModel
	{
		public SendSuccessViewModel(SmartTransaction finalTransaction)
		{
			NextCommand = ReactiveCommand.Create(OnNext);

			SetupCancel(enableCancel: false, enableCancelOnEscape: true, enableCancelOnPressed: true);
		}

		private void OnNext()
		{
			Navigate().Clear();
		}
	}
}
