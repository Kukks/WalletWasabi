using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace WalletWasabi.Gui.Suggestions
{
	public class SuggestionBehavior : Behavior<TextBox>
	{
		public static readonly AvaloniaProperty<IEnumerable<SuggestionViewModel>> SuggestionItemsProperty =
			AvaloniaProperty.Register<SuggestionBehavior, IEnumerable<SuggestionViewModel>>(nameof(SuggestionItems), defaultBindingMode: BindingMode.TwoWay);

		public IEnumerable<SuggestionViewModel> SuggestionItems
		{
			get => GetValue(SuggestionItemsProperty);
			set => SetValue(SuggestionItemsProperty, value);
		}

		private CompositeDisposable Disposables { get; set; }

		protected override void OnAttached()
		{
			Disposables = new CompositeDisposable();

			base.OnAttached();

			Disposables.Add(AssociatedObject.AddHandler(
				InputElement.KeyDownEvent,
				(sender, e) =>
				{
					if (e.Key == Key.Tab)
					{
						HandleAutoUpdate();
						e.Handled = true;
					}
					else if (e.Key == Key.Down)
					{
						if (SuggestionItems is { })
						{
							if (SuggestionItems.All(x => !x.IsHighLighted))
							{
								var item = SuggestionItems.FirstOrDefault();
								if (item is { })
								{
									item.IsHighLighted = true;
								}
							}
							else
							{
								var index = SuggestionItems.Select((v, i) => new { sugg = v, index = i })?.FirstOrDefault(x => x.sugg.IsHighLighted)?.index;
								if (index is { })
								{
									var suggItemsArray = SuggestionItems.ToArray();
									suggItemsArray[index.Value].IsHighLighted = false;
									index++;
									if (suggItemsArray.Length <= index.Value)
									{
										index = 0;
									}
									suggItemsArray[index.Value].IsHighLighted = true;
								}
							}

							e.Handled = true;
						}
					}
					else if (e.Key == Key.Up)
					{
						if (SuggestionItems is { })
						{
							foreach (var item in SuggestionItems)
							{
								item.IsHighLighted = false;
							}
							e.Handled = true;
						}
					}
					else if (e.Key == Key.Enter)
					{
						if (SuggestionItems is { })
						{
							foreach (var item in SuggestionItems)
							{
								if (item.IsHighLighted)
								{
									item.OnSelected();
									break;
								}
							}
							e.Handled = true;
						}
					}
				}));
		}

		protected override void OnDetaching()
		{
			base.OnDetaching();

			Disposables?.Dispose();
		}

		private void HandleAutoUpdate()
		{
			SuggestionItems?.FirstOrDefault()?.OnSelected();
		}
	}
}