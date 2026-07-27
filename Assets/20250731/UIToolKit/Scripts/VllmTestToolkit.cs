using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class VllmTestToolkit : MonoBehaviour
{
    [SerializeField] private string apiUrl = "http://localhost:8000/v1/chat/completions";
    [SerializeField] private string modelName = "Qwen/Qwen2.5-7B-Instruct-AWQ";

    private UIDocument _uiDocument;

    private Label     _chat1Response;
    private ScrollView _chat1ScrollView;
    private TextField  _chat1Prompt;
    private Button     _chat1Button;
    private Button     _chat1ClearButton;

    private Label     _chat2Response;
    private ScrollView _chat2ScrollView;
    private TextField  _chat2Prompt;
    private Button     _chat2Button;
    private Button     _chat2ClearButton;

    private static readonly HttpClient _httpClient = new HttpClient
    {
        Timeout = System.Threading.Timeout.InfiniteTimeSpan
    };

    private CancellationTokenSource _chat1Cts;
    private CancellationTokenSource _chat2Cts;

    private SynchronizationContext _mainThreadContext;

    private void Awake()
    {
        _uiDocument = GetComponent<UIDocument>();
        _mainThreadContext = SynchronizationContext.Current;
    }

    private void OnEnable()
    {
        VisualElement root = _uiDocument.rootVisualElement;

        _chat1ScrollView  = root.Q<ScrollView>("Chat1_scrollView");
        _chat1Response    = root.Q<Label>("Chat1_responseLabel");
        _chat1Prompt      = root.Q<TextField>("Chat1_Prompt");
        _chat1Button      = root.Q<Button>("Chat1_btn");
        _chat1ClearButton = root.Q<Button>("Chat1_clearBtn");

        _chat2ScrollView  = root.Q<ScrollView>("Chat2_scrollView");
        _chat2Response    = root.Q<Label>("Chat2_responseLabel");
        _chat2Prompt      = root.Q<TextField>("Chat2_Prompt");
        _chat2Button      = root.Q<Button>("Chat2_btn");
        _chat2ClearButton = root.Q<Button>("Chat2_clearBtn");

        _chat1Button.clicked      += OnChat1Clicked;
        _chat2Button.clicked      += OnChat2Clicked;
        _chat1ClearButton.clicked += OnChat1ClearClicked;
        _chat2ClearButton.clicked += OnChat2ClearClicked;
    }

    private void OnDisable()
    {
        if (_chat1Button != null)      _chat1Button.clicked      -= OnChat1Clicked;
        if (_chat2Button != null)      _chat2Button.clicked      -= OnChat2Clicked;
        if (_chat1ClearButton != null) _chat1ClearButton.clicked -= OnChat1ClearClicked;
        if (_chat2ClearButton != null) _chat2ClearButton.clicked -= OnChat2ClearClicked;

        _chat1Cts?.Cancel();
        _chat2Cts?.Cancel();
    }

    private void OnChat1Clicked()
    {
        string prompt = _chat1Prompt.value;
        if (string.IsNullOrWhiteSpace(prompt)) return;

        _chat1Cts?.Cancel();
        _chat1Cts = new CancellationTokenSource();
        _chat1Button.SetEnabled(false);
        _chat1Response.text = string.Empty;

        _ = SendChatRequestAsync(prompt, _chat1Response, _chat1ScrollView, _chat1Button, _chat1Cts.Token);
    }

    private void OnChat2Clicked()
    {
        string prompt = _chat2Prompt.value;
        if (string.IsNullOrWhiteSpace(prompt)) return;

        _chat2Cts?.Cancel();
        _chat2Cts = new CancellationTokenSource();
        _chat2Button.SetEnabled(false);
        _chat2Response.text = string.Empty;

        _ = SendChatRequestAsync(prompt, _chat2Response, _chat2ScrollView, _chat2Button, _chat2Cts.Token);
    }

    private void OnChat1ClearClicked()
    {
        _chat1Cts?.Cancel();
        _chat1Response.text = string.Empty;
        _chat1Button.SetEnabled(true);
    }

    private void OnChat2ClearClicked()
    {
        _chat2Cts?.Cancel();
        _chat2Response.text = string.Empty;
        _chat2Button.SetEnabled(true);
    }

    private async Task SendChatRequestAsync(string prompt, Label responseLabel, ScrollView scrollView, Button button, CancellationToken cancellationToken)
    {
        try
        {
            string requestJson = BuildRequestJson(prompt);
            using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
            request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream, Encoding.UTF8);

            var sb = new StringBuilder();
            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                string line = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ")) continue;

                string data = line.Substring(6);
                if (data == "[DONE]") break;

                string token = ParseDeltaContent(data);
                if (token == null) continue;

                sb.Append(token);
                string currentText = sb.ToString();
                _mainThreadContext.Post(_ =>
                {
                    responseLabel.text = currentText;
                    scrollView.schedule.Execute(() =>
                        scrollView.verticalScroller.value = scrollView.verticalScroller.highValue);
                }, null);
            }
        }
        catch (OperationCanceledException)
        {
            // Request was cancelled
        }
        catch (Exception ex)
        {
            string errorMsg = $"Error: {ex.Message}";
            _mainThreadContext.Post(_ => responseLabel.text = errorMsg, null);
            Debug.LogError($"[VllmTestToolkit] {ex}");
        }
        finally
        {
            _mainThreadContext.Post(_ => button.SetEnabled(true), null);
        }
    }

    private string BuildRequestJson(string prompt)
    {
        string escapedModel  = EscapeJson(modelName);
        string escapedPrompt = EscapeJson(prompt);
        return $"{{\"model\":\"{escapedModel}\",\"messages\":[{{\"role\":\"user\",\"content\":\"{escapedPrompt}\"}}],\"stream\":true}}";
    }

    private static string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    private static string ParseDeltaContent(string jsonData)
    {
        // Extracts the token text from a streaming SSE chunk:
        // {"choices":[{"delta":{"content":"TOKEN"}, ...}]}
        const string contentKey = "\"content\":\"";
        int idx = jsonData.IndexOf(contentKey, StringComparison.Ordinal);
        if (idx < 0) return null;

        int start = idx + contentKey.Length;
        var sb = new StringBuilder();
        for (int i = start; i < jsonData.Length; i++)
        {
            char c = jsonData[i];
            if (c == '\\' && i + 1 < jsonData.Length)
            {
                char next = jsonData[i + 1];
                switch (next)
                {
                    case '"':  sb.Append('"');  i++; break;
                    case '\\': sb.Append('\\'); i++; break;
                    case 'n':  sb.Append('\n'); i++; break;
                    case 'r':  sb.Append('\r'); i++; break;
                    case 't':  sb.Append('\t'); i++; break;
                    default:   sb.Append(c);        break;
                }
            }
            else if (c == '"')
            {
                break;
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
