using AIDevGallery.Sample.Utils;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Text;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Sample;

internal sealed partial class Sample : Microsoft.UI.Xaml.Controls.Page
{
    private const int MaxLength = 1000;
    private bool _isProgressVisible;
    private LanguageModel? _languageModel;
    private CancellationTokenSource? _cts;

    public Sample()
    {
        this.Unloaded += (s, e) => CleanUp();
        this.InitializeComponent();
    }

    protected override async void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        var readyState = LanguageModel.GetReadyState();
        if (readyState is AIFeatureReadyState.Ready or AIFeatureReadyState.NotReady)
        {
            if (readyState == AIFeatureReadyState.NotReady)
            {
                var operation = await LanguageModel.EnsureReadyAsync();

                if (operation.Status != AIFeatureReadyResultState.Success)
                {
                    App.Window?.ShowException(null, $"Phi-Silica is not available");
                }
            }

            _ = GenerateText(InputTextBox.Text);
        }
        else
        {
            var msg = readyState == AIFeatureReadyState.DisabledByUser
                ? "Disabled by user."
                : "Not supported on this system.";
            App.Window?.ShowException(null, $"Phi-Silica is not available: {msg}");
        }

        App.Window?.ModelLoaded();
    }

    private void CleanUp()
    {
        CancelGeneration();
        _languageModel?.Dispose();
    }

    public bool IsProgressVisible
    {
        get => _isProgressVisible;
        set
        {
            _isProgressVisible = value;
            DispatcherQueue.TryEnqueue(() =>
            {
                OutputProgressBar.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                StopIcon.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
            });
        }
    }

    public async Task GenerateText(string prompt)
    {
        GenerateTextBlock.Text = string.Empty;
        GenerateButton.Visibility = Visibility.Collapsed;
        StopBtn.Visibility = Visibility.Visible;
        IsProgressVisible = true;
        InputTextBox.IsEnabled = false;

        IsProgressVisible = true;

        _languageModel ??= await LanguageModel.CreateAsync();
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        var operation = _languageModel.GenerateResponseAsync(prompt);
        operation.Progress = (asyncInfo, delta) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_isProgressVisible)
                {
                    StopBtn.Visibility = Visibility.Visible;
                    IsProgressVisible = false;
                }

                GenerateTextBlock.Text += delta;
                if (_cts?.IsCancellationRequested == true)
                {
                    operation.Cancel();
                }
            });
        };

        var result = await operation;

        StopBtn.Visibility = Visibility.Collapsed;
        GenerateButton.Visibility = Visibility.Visible;
        InputTextBox.IsEnabled = true;
        _cts?.Dispose();
        _cts = null;
    }

    private void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        if (this.InputTextBox.Text.Length > 0)
        {
            _ = GenerateText(InputTextBox.Text);
        }
    }

    private void TextBox_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter && sender is TextBox)
        {
            if (InputTextBox.Text.Length > 0)
            {
                _ = GenerateText(InputTextBox.Text);
            }
        }
    }

    private void CancelGeneration()
    {
        StopBtn.Visibility = Visibility.Collapsed;
        IsProgressVisible = false;
        GenerateButton.Visibility = Visibility.Visible;
        InputTextBox.IsEnabled = true;
        _cts?.Cancel();
    }

    private void StopBtn_Click(object sender, RoutedEventArgs e)
    {
        CancelGeneration();
    }

    private void InputBox_Changed(object sender, TextChangedEventArgs e)
    {
        var inputLength = InputTextBox.Text.Length;
        if (inputLength > 0)
        {
            if (inputLength >= MaxLength)
            {
                InputTextBox.Description = $"{inputLength} of {MaxLength}. Max characters reached.";
            }
            else
            {
                InputTextBox.Description = $"{inputLength} of {MaxLength}";
            }

            GenerateButton.IsEnabled = inputLength <= MaxLength;
        }
        else
        {
            InputTextBox.Description = string.Empty;
            GenerateButton.IsEnabled = false;
        }
    }
}