using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace Svg.Editor.Skia.Uno;

public sealed partial class SvgEditorWorkspace
{
    private IEditorMcpConnection? _mcpConnection;

    /// <summary>Assign a platform connection controller to enable the MCP command.</summary>
    public IEditorMcpConnection? McpConnection
    {
        get => _mcpConnection;
        set
        {
            if (ReferenceEquals(_mcpConnection, value))
                return;
            if (_mcpConnection is not null)
                _mcpConnection.StateChanged -= OnMcpConnectionStateChanged;
            _mcpConnection = value;
            if (value is not null)
                value.StateChanged += OnMcpConnectionStateChanged;
            RefreshEditorMenus();
        }
    }

    private void BuildMcpConnectionCommand()
    {
        _commands.Add(new EditorCommand("mcp-connection", "MCP Connection…", "Window", string.Empty,
            ShowMcpConnectionAsync)
        { CanExecute = () => McpConnection is not null });
    }

    private void OnMcpConnectionStateChanged(object? sender, EventArgs args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!_disposed && McpConnection is { } connection)
                StatusText.Text = "MCP: " + connection.State.Status;
        });
    }

    private async Task ShowMcpConnectionAsync()
    {
        if (CurrentMcpInteractionCancellation.CanBeCanceled)
            throw new InvalidOperationException("MCP connection lifecycle controls must be opened directly in the workspace.");
        if (McpConnection is not { } connection)
            return;

        var modes = connection.SupportsLocalServer
            ? new[] { "Local MCP server" }
            : new[] { "Connect to bridge" };
        var stdioMode = new[] { "Standard input/output" };
        var mode = new ComboBox
        {
            Header = "Connection",
            ItemsSource = modes,
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var initial = connection.State;
        var address = new TextBox
        {
            Header = "Bridge WebSocket address",
            Text = !connection.SupportsLocalServer && !string.IsNullOrEmpty(initial.Endpoint)
            ? initial.Endpoint : "ws://127.0.0.1:52742/editor"
        };
        var token = new PasswordBox { Header = "Bridge attachment token", PasswordRevealMode = PasswordRevealMode.Peek };
        var port = new NumberBox
        {
            Header = "Local port",
            Value = Uri.TryCreate(initial.Endpoint, UriKind.Absolute, out var initialEndpoint)
            && initialEndpoint.Scheme is "http" or "https" ? initialEndpoint.Port : 52741,
            Minimum = 1024,
            Maximum = 65535,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact
        };
        var readOnly = new CheckBox { Content = "Allow inspection and export only", IsChecked = initial.ReadOnly };
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
        AutomationProperties.SetLiveSetting(status, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
        var endpoint = new TextBox { Header = "MCP endpoint", IsReadOnly = true };
        var clientToken = new PasswordBox { Header = "MCP client bearer token", PasswordRevealMode = PasswordRevealMode.Peek };
        var content = new StackPanel { Spacing = 12, Width = DialogContentWidth(580) };
        content.Children.Add(StudioHint("Connect an MCP client to this live workspace. Close this dialog before editing through the connection. Changes use normal document history. Connection tokens are kept only for this session."));
        foreach (var control in new FrameworkElement[] { mode, address, token, port, readOnly, status, endpoint, clientToken })
            content.Children.Add(control);
        var dialog = MakeDialog("MCP Connection", content, "Connect", "Close");
        dialog.SecondaryButtonText = "Copy client configuration";
        var busy = false;
        string? validationError = null;

        bool Local() => connection.SupportsLocalServer && mode.SelectedIndex == 0;
        void Update()
        {
            var state = connection.State;
            var connected = state.IsConnected;
            if (connected)
                readOnly.IsChecked = state.ReadOnly;
            var shownModes = connected && state.Endpoint == "stdio" ? stdioMode : modes;
            if (!ReferenceEquals(mode.ItemsSource, shownModes))
                mode.ItemsSource = shownModes;
            if (mode.SelectedIndex != 0)
                mode.SelectedIndex = 0;
            mode.IsEnabled = !connected && !busy;
            address.IsEnabled = token.IsEnabled = port.IsEnabled = readOnly.IsEnabled = !connected && !busy;
            address.Visibility = token.Visibility = Local() ? Visibility.Collapsed : Visibility.Visible;
            port.Visibility = Local() ? Visibility.Visible : Visibility.Collapsed;
            status.Text = validationError ?? state.Status;
            endpoint.Text = state.Endpoint;
            endpoint.Visibility = string.IsNullOrEmpty(state.Endpoint) ? Visibility.Collapsed : Visibility.Visible;
            clientToken.Password = state.Token;
            clientToken.Visibility = string.IsNullOrEmpty(state.Token) ? Visibility.Collapsed : Visibility.Visible;
            dialog.PrimaryButtonText = connected ? "Disconnect" : Local() ? "Start server" : "Connect";
            dialog.IsPrimaryButtonEnabled = !busy;
            dialog.IsSecondaryButtonEnabled = !busy && !string.IsNullOrEmpty(state.ClientConfiguration);
        }
        mode.SelectionChanged += (_, _) => Update();
        EventHandler changed = (_, _) => DispatcherQueue.TryEnqueue(Update);
        connection.StateChanged += changed;
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            args.Cancel = true;
            if (busy)
                return;
            var deferral = args.GetDeferral();
            validationError = null;
            busy = true;
            Update();
            try
            {
                if (connection.State.IsConnected)
                    await connection.StopAsync();
                else
                {
                    if (!double.IsFinite(port.Value) || port.Value != Math.Truncate(port.Value))
                        throw new ArgumentException("The local port must be a whole number.");
                    await connection.StartAsync(new EditorMcpConnectionSettings(
                        Local() ? EditorMcpConnectionMode.LocalServer : EditorMcpConnectionMode.BrowserBridge,
                        address.Text.Trim(), token.Password, (int)port.Value, readOnly.IsChecked == true));
                    token.Password = string.Empty;
                }
            }
            catch (Exception error)
            {
                validationError = error.Message;
            }
            finally
            {
                busy = false;
                Update();
                deferral.Complete();
            }
        };
        dialog.SecondaryButtonClick += (_, args) =>
        {
            args.Cancel = true;
            if (string.IsNullOrEmpty(connection.State.ClientConfiguration))
                return;
            var data = new DataPackage();
            data.SetText(connection.State.ClientConfiguration);
            Clipboard.SetContent(data);
            status.Text = "Client configuration copied. It contains this session's bearer token.";
        };
        Update();
        try
        {
            await ShowDialogAsync(dialog);
        }
        finally
        {
            connection.StateChanged -= changed;
            token.Password = clientToken.Password = string.Empty;
        }
    }

    private void DetachMcpConnection()
    {
        if (_mcpConnection is not null)
            _mcpConnection.StateChanged -= OnMcpConnectionStateChanged;
        _mcpConnection = null;
    }
}
