using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using KAYRON.Core;
using KAYRON.Engine;

namespace KAYRON.UI;

public partial class MainWindow : Window
{
    private readonly GerenciadorConversa _gerenciador;
    private KAYRON.Engine.Contexto _contexto = new();
    private bool _processando;
    private bool _responderEmAudio;
    private bool _vozAutorizadaNaSessao;
    private bool _modoVozAtivo;
    private CancellationTokenSource? _modoVozCts;
    private CancellationTokenSource? _processamentoCts;
    private readonly List<SessaoChat> _sessoes = [];
    private SessaoChat _sessaoAtiva;
    private readonly string _arquivoSessoes = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KAYRON", "conversas.json");

    private sealed class DadosSessoes
    {
        public DadosSessoes() { }
        public int SessaoAtiva { get; set; }
        public List<DadosSessao> Sessoes { get; set; } = [];
    }
    private sealed class DadosSessao
    {
        public DadosSessao() { }
        public string Titulo { get; set; } = "Nova conversa";
        public List<MensagemSessao> Mensagens { get; set; } = [];
    }

    private sealed class SessaoChat(string titulo)
    {
        public string Titulo { get; set; } = titulo;
        public KAYRON.Engine.Contexto Contexto { get; } = new();
        public List<MensagemSessao> Mensagens { get; } = [];
    }

    private sealed record MensagemSessao(bool Usuario, string Texto);

    public MainWindow(GerenciadorConversa gerenciador)
    {
        InitializeComponent();
        _gerenciador = gerenciador ?? throw new ArgumentNullException(nameof(gerenciador));
        _sessaoAtiva = new SessaoChat("Nova conversa");
        _contexto = _sessaoAtiva.Contexto;
        if (!CarregarSessoes()) _sessoes.Add(_sessaoAtiva);
        _contexto = _sessaoAtiva.Contexto;
        RestaurarHistoricoEngine(_sessaoAtiva);
        RenderizarSessao(_sessaoAtiva);
        AtualizarHistoricoVisual();
        InputBox.Focus();
        Loaded += (_, _) => AtualizarLayout();
    }

    private bool CarregarSessoes()
    {
        try
        {
            if (!File.Exists(_arquivoSessoes)) return false;
            var dados = JsonSerializer.Deserialize<DadosSessoes>(File.ReadAllText(_arquivoSessoes));
            if (dados?.Sessoes is not { Count: > 0 }) return false;
            _sessoes.Clear();
            foreach (var item in dados.Sessoes.Take(100))
            {
                var sessao = new SessaoChat(string.IsNullOrWhiteSpace(item.Titulo) ? "Nova conversa" : item.Titulo);
                sessao.Mensagens.AddRange(item.Mensagens.Where(m => m is not null && m.Texto is not null).Take(2000));
                _sessoes.Add(sessao);
            }
            if (_sessoes.Count == 0) return false;
            _sessaoAtiva = _sessoes[Math.Clamp(dados.SessaoAtiva, 0, _sessoes.Count - 1)];
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível restaurar o histórico local. Uma conversa nova será iniciada.\n\n{ex.Message}",
                "Hist?rico do KAYRON", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    private void SalvarSessoes()
    {
        try
        {
            var pasta = Path.GetDirectoryName(_arquivoSessoes)!;
            Directory.CreateDirectory(pasta);
            var dados = new DadosSessoes
            {
                SessaoAtiva = Math.Max(0, _sessoes.IndexOf(_sessaoAtiva)),
                Sessoes = _sessoes.Select(s => new DadosSessao { Titulo = s.Titulo, Mensagens = [.. s.Mensagens] }).ToList()
            };
            var temporario = _arquivoSessoes + ".tmp";
            File.WriteAllText(temporario, JsonSerializer.Serialize(dados));
            File.Move(temporario, _arquivoSessoes, true);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível salvar o histórico local.\n\n{ex.Message}",
                "Hist?rico do KAYRON", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Configuracoes_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        var configuracoes = new MenuItem { Header = "Configurações" };
        configuracoes.Click += (_, _) => AbrirPainelConfiguracoes();
        var integracoes = new MenuItem { Header = "Integrações" };
        integracoes.Click += (_, _) => Modelo_Click(sender, e);
        menu.Items.Add(configuracoes);
        menu.Items.Add(integracoes);
        menu.PlacementTarget = SettingsButton;
        menu.IsOpen = true;
    }

    private void AbrirPainelConfiguracoes()
    {
        var painel = new Window
        {
            Title = "Configurações do KAYRON", Owner = this, Width = 390, SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = new SolidColorBrush(Color.FromRgb(24, 19, 30)),
            Foreground = Brushes.White, ResizeMode = ResizeMode.NoResize
        };
        var conteudo = new StackPanel { Margin = new Thickness(22) };
        conteudo.Children.Add(new TextBlock { Text = "Configurações", FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) });
        conteudo.Children.Add(new TextBlock { Text = "Prefer?ncias atuais da sess?o", FontWeight = FontWeights.SemiBold });
        conteudo.Children.Add(new TextBlock { Text = $"Conversa por voz: {(_modoVozAtivo ? "ativa" : "desligada")}\nRespostas em ?udio: {(_responderEmAudio ? "ativas" : "desligadas")}\nHist?rico local: t?tulos e transcri??es salvos neste dispositivo.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 18) });
        var fechar = new Button { Content = "Fechar", HorizontalAlignment = HorizontalAlignment.Right, Padding = new Thickness(14, 6, 14, 6) };
        fechar.Click += (_, _) => painel.Close();
        conteudo.Children.Add(fechar);
        painel.Content = conteudo;
        painel.ShowDialog();
    }

    private async void Enviar_Click(object sender, RoutedEventArgs e) => await EnviarMensagemAsync();

    private async void Voz_Click(object sender, RoutedEventArgs e)
    {
        if (_modoVozAtivo)
        {
            _modoVozAtivo = false;
            _modoVozCts?.Cancel();
            _processamentoCts?.Cancel();
            VoiceButton.Content = "🎙";
            VoiceButton.ToolTip = "Ativar conversa por voz";
            DefinirStatusVoz("Voz desligada", "#77717D");
            InputBox.Focus();
            return;
        }

        if (_processando) return;

        if (!_vozAutorizadaNaSessao)
        {
            var confirmar = MessageBox.Show(
                "Ao ativar, o KAYRON captará áudio pelo microfone para transcrever suas perguntas. O modo ficará ativo até você desligá-lo no botão.",
                "KAYRON — Conversa por voz",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Information);

            if (confirmar != MessageBoxResult.OK) return;
            _vozAutorizadaNaSessao = true;
        }

        _modoVozAtivo = true;
        _modoVozCts = new CancellationTokenSource();
        _responderEmAudio = true;
        VoiceButton.Content = "⏹";
        VoiceButton.ToolTip = "Desativar conversa por voz e voltar ao texto";
        DefinirStatusVoz("Preparando microfone…", "#E0A040");

        try
        {
            while (_modoVozAtivo)
            {
                VoiceButton.Content = "⏹";
                DefinirStatusVoz("Preparando microfone…", "#E0A040");
                var transcricao = await CapturarTranscricaoVozAsync(_modoVozCts.Token);

                if (!_modoVozAtivo) break;
                if (string.IsNullOrWhiteSpace(transcricao)) continue;

                InputBox.Text = transcricao;
                DefinirStatusVoz("Processando resposta…", "#A78BFA");
                await EnviarMensagemAsync();
                if (_modoVozAtivo)
                {
                    VoiceButton.Content = "⏹";
                    DefinirStatusVoz("Preparando próxima pergunta…", "#E0A040");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelamento esperado quando o usuário desliga o modo de voz.
        }
        catch (Exception ex)
        {
            if (_modoVozAtivo)
                MessageBox.Show($"Não foi possível continuar a conversa por voz.\n\n{ex.Message}",
                    "KAYRON — Voz", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _modoVozAtivo = false;
            _responderEmAudio = false;
            _modoVozCts?.Dispose();
            _modoVozCts = null;
            VoiceButton.Content = "🎙";
            VoiceButton.ToolTip = "Ativar conversa por voz";
            VoiceButton.IsEnabled = true;
            DefinirStatusVoz("Voz desligada", "#77717D");
            InputBox.Focus();
        }
    }

    private async Task<string?> CapturarTranscricaoVozAsync(CancellationToken token)
    {
        var script = System.IO.Path.Combine(AppContext.BaseDirectory, "kayron_voice_input.py");
        if (!System.IO.File.Exists(script))
            throw new System.IO.FileNotFoundException("O componente de voz não foi encontrado.", script);

        var inicio = new ProcessStartInfo
        {
            FileName = @"C:\Python314\python.exe",
            Arguments = $"-u \"{script}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var processo = new Process { StartInfo = inicio };
        var linhasSaida = new List<string>();
        var travaSaida = new object();
        processo.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            lock (travaSaida) linhasSaida.Add(e.Data);
            if (e.Data == "KAYRON_LISTENING=1")
                Dispatcher.Invoke(() => DefinirStatusVoz("Ouvindo agora — pode falar", "#35D07F"));
        };

        if (!processo.Start())
            throw new InvalidOperationException("Não foi possível iniciar a captura de voz.");

        using var registroCancelamento = token.Register(() =>
        {
            try { if (!processo.HasExited) processo.Kill(true); }
            catch { }
        });

        processo.BeginOutputReadLine();
        var erroTask = processo.StandardError.ReadToEndAsync();
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(token);
        limite.CancelAfter(TimeSpan.FromSeconds(60));
        await processo.WaitForExitAsync(limite.Token);

        string saida;
        lock (travaSaida) saida = string.Join(Environment.NewLine, linhasSaida);
        var erro = await erroTask;
        var transcricao = saida
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault(linha => linha.StartsWith("KAYRON_TRANSCRIPT=", StringComparison.Ordinal))
            ?.Substring("KAYRON_TRANSCRIPT=".Length)
            .Trim();

        if (processo.ExitCode != 0)
        {
            if (token.IsCancellationRequested) token.ThrowIfCancellationRequested();
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(erro)
                    ? "Não foi possível transcrever a fala. Tente novamente."
                    : erro.Trim());
        }

        return transcricao;
    }
    private void DefinirStatusVoz(string texto, string cor)
    {
        VoiceStatusText.Text = texto;
        VoiceStatusDot.Fill = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(cor));
    }

    private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        InputPlaceholder.Visibility = string.IsNullOrEmpty(InputBox.Text)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void InputBox_GotFocus(object sender, RoutedEventArgs e)
    {
        InputPlaceholder.Visibility = Visibility.Collapsed;
    }

    private void InputBox_LostFocus(object sender, RoutedEventArgs e)
    {
        InputPlaceholder.Visibility = string.IsNullOrEmpty(InputBox.Text)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
        {
            e.Handled = true;
            await EnviarMensagemAsync();
        }
    }

    private async Task EnviarMensagemAsync()
    {
        if (_processando) return;
        var texto = InputBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(texto)) return;

        _processando = true;
        SendButton.IsEnabled = false;
        _processamentoCts?.Dispose();
        var processamento = new CancellationTokenSource();
        _processamentoCts = processamento;
        var token = processamento.Token;
        InputBox.Clear();
        WelcomePanel.Visibility = Visibility.Collapsed;

        var sessao = _sessaoAtiva;
        AtualizarTituloSessao(sessao, texto);
        sessao.Mensagens.Add(new MensagemSessao(true, texto));
        SalvarSessoes();
        AdicionarMensagem(texto, true);

        var carregando = AdicionarMensagem("KAYRON está processando…", false, true);

        try
        {
            var resposta = await _gerenciador.ProcessarAsync(texto, _contexto, token);
            sessao.Mensagens.Add(new MensagemSessao(false, resposta.Conteudo));
            SalvarSessoes();
            if (ReferenceEquals(_sessaoAtiva, sessao))
                carregando.Child = CriarConteudoMensagem(resposta.Conteudo, false, true);
            if (_responderEmAudio)
            {
                try
                {
                    await FalarRespostaAsync(resposta.Conteudo, token);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"A resposta foi gerada, mas não foi possível reproduzir o áudio.\n\n{ex.Message}",
                        "KAYRON — Saída de voz", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
        catch (OperationCanceledException)
        {
            const string mensagem = "Operação cancelada.";
            sessao.Mensagens.Add(new MensagemSessao(false, mensagem));
            SalvarSessoes();
            if (ReferenceEquals(_sessaoAtiva, sessao))
                carregando.Child = CriarConteudoMensagem(mensagem, false, true);
        }
        catch (Exception ex)
        {
            var mensagem = $"Não foi possível concluir a operação.\n\n{ex.Message}";
            sessao.Mensagens.Add(new MensagemSessao(false, mensagem));
            SalvarSessoes();
            if (ReferenceEquals(_sessaoAtiva, sessao))
                carregando.Child = CriarConteudoMensagem(mensagem, false, true);
        }
        finally
        {
            if (ReferenceEquals(_processamentoCts, processamento))
            {
                _processando = false;
                SendButton.IsEnabled = true;
                _processamentoCts.Dispose();
                _processamentoCts = null;
            }
            if (ReferenceEquals(_sessaoAtiva, sessao))
            {
                InputBox.Focus();
                ChatScroll.ScrollToEnd();
            }
        }
    }

    private async Task FalarRespostaAsync(string texto, CancellationToken token)
    {
        var script = System.IO.Path.Combine(AppContext.BaseDirectory, "kayron_voice_output.py");
        if (!System.IO.File.Exists(script))
            throw new System.IO.FileNotFoundException("O componente de saída de voz não foi encontrado.", script);

        var inicio = new ProcessStartInfo
        {
            FileName = @"C:\Python314\python.exe",
            Arguments = $"-u \"{script}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var processo = new Process { StartInfo = inicio };
        if (!processo.Start())
            throw new InvalidOperationException("Não foi possível iniciar a saída de voz.");

        var saidaTask = processo.StandardOutput.ReadToEndAsync();
        var erroTask = processo.StandardError.ReadToEndAsync();
        await processo.StandardInput.WriteAsync(texto);
        processo.StandardInput.Close();
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(token);
        limite.CancelAfter(TimeSpan.FromSeconds(60));
        await processo.WaitForExitAsync(limite.Token);
        var saida = await saidaTask;
        var erro = await erroTask;

        if (processo.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(erro)
                ? $"O sintetizador de voz falhou. {saida}".Trim()
                : erro.Trim());

        var linhaAudio = saida.Trim();
        var arquivoAudio = linhaAudio.StartsWith("KAYRON_TTS_FILE=", StringComparison.Ordinal)
            ? linhaAudio.Substring("KAYRON_TTS_FILE=".Length).Trim()
            : null;

        if (string.IsNullOrWhiteSpace(arquivoAudio) || !System.IO.File.Exists(arquivoAudio))
            throw new InvalidOperationException("O sintetizador não retornou o arquivo de áudio esperado.");

        var player = new MediaPlayer();
        var reproducao = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        player.MediaOpened += (_, _) => player.Play();
        player.MediaEnded += (_, _) => reproducao.TrySetResult(true);
        player.MediaFailed += (_, e) => reproducao.TrySetException(
            e.ErrorException ?? new InvalidOperationException("Falha ao reproduzir a resposta de voz."));

        try
        {
            player.Open(new Uri(arquivoAudio, UriKind.Absolute));
            using var limiteAudio = CancellationTokenSource.CreateLinkedTokenSource(token);
            limiteAudio.CancelAfter(TimeSpan.FromSeconds(120));
            await reproducao.Task.WaitAsync(limiteAudio.Token);
        }
        finally
        {
            player.Stop();
            player.Close();
        }
    }
    private void AtualizarTituloSessao(SessaoChat sessao, string texto)
    {
        if (sessao.Mensagens.Count > 0) return;
        var titulo = Regex.Replace(texto.Trim(), @"\s+", " ");
        sessao.Titulo = titulo.Length > 42 ? titulo[..39] + "…" : titulo;
        AtualizarHistoricoVisual();
    }

    private void AtualizarHistoricoVisual()
    {
        HistoryList.Children.Clear();

        foreach (var sessao in _sessoes.AsEnumerable().Reverse())
        {
            var linha = new Grid
            {
                Margin = new Thickness(2, 1, 2, 1)
            };

            linha.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            linha.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var botao = new Button
            {
                Content = sessao.Titulo,
                Style = (Style)FindResource("HistoryButton"),
                ToolTip = sessao.Titulo,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                MaxWidth = 220
            };

            botao.Click += (_, _) => AbrirChat(sessao);

            var menu = new Button
            {
                Content = "⋯",
                Style = (Style)FindResource("HistoryButton"),
                ToolTip = "Opções do histórico",
                FontSize = 18,
                Padding = new Thickness(6, 2, 6, 2),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            var menuContexto = new ContextMenu();
            var excluir = new MenuItem { Header = "Excluir conversa" };
            excluir.Click += (_, _) => ExcluirHistorico(sessao);
            menuContexto.Items.Add(excluir);
            menu.Click += (_, _) => { menuContexto.PlacementTarget = menu; menuContexto.IsOpen = true; };

            Grid.SetColumn(botao, 0);
            Grid.SetColumn(menu, 1);
            linha.Children.Add(botao);
            linha.Children.Add(menu);
            HistoryList.Children.Add(linha);
        }
    }

    private void ExcluirHistorico(SessaoChat sessao)
    {
        if (MessageBox.Show($"Excluir a conversa ‘{sessao.Titulo}’?", "Excluir conversa",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        var eraAtiva = ReferenceEquals(sessao, _sessaoAtiva);
        _sessoes.Remove(sessao);
        SalvarSessoes();
        if (!eraAtiva)
        {
            AtualizarHistoricoVisual();
            return;
        }

        _processamentoCts?.Cancel();
        _processamentoCts?.Dispose();
        _processamentoCts = null;
        _processando = false;

        App.Services.GetRequiredService<IHistoricoConversa>().Limpar();
        IniciarNovaSessao();
    }

    private void AbrirChat(SessaoChat sessao)
    {
        if (_processando || ReferenceEquals(sessao, _sessaoAtiva)) return;
        _sessaoAtiva = sessao;
        _contexto = sessao.Contexto;
        RestaurarHistoricoEngine(sessao);
        RenderizarSessao(sessao);
        AtualizarHistoricoVisual();
        SalvarSessoes();
    }

    private void RestaurarHistoricoEngine(SessaoChat sessao)
    {
        var historico = App.Services.GetRequiredService<IHistoricoConversa>();
        historico.Limpar();
        foreach (var mensagem in sessao.Mensagens)
        {
            if (mensagem.Usuario) historico.AdicionarUsuario(mensagem.Texto);
            else historico.AdicionarAssistente(mensagem.Texto);
        }
    }

    private void RenderizarSessao(SessaoChat sessao)
    {
        MessagesPanel.Children.Clear();
        if (sessao.Mensagens.Count == 0)
        {
            MessagesPanel.Children.Add(WelcomePanel);
            WelcomePanel.Visibility = Visibility.Visible;
        }
        else
        {
            WelcomePanel.Visibility = Visibility.Collapsed;
            foreach (var mensagem in sessao.Mensagens)
                AdicionarMensagem(mensagem.Texto, mensagem.Usuario);
        }
        InputBox.Clear();
        InputBox.Focus();
        ChatScroll.ScrollToEnd();
    }

    private void IniciarNovaSessao()
    {
        SendButton.IsEnabled = true;
        _sessaoAtiva = new SessaoChat("Nova conversa");
        _sessoes.Add(_sessaoAtiva);
        _contexto = _sessaoAtiva.Contexto;
        MessagesPanel.Children.Clear();
        MessagesPanel.Children.Add(WelcomePanel);
        WelcomePanel.Visibility = Visibility.Visible;
        InputBox.Clear();
        InputBox.Focus();
        AtualizarHistoricoVisual();
        SalvarSessoes();
    }

    private Border AdicionarMensagem(string texto, bool usuario, bool carregando = false)
    {
        var border = new Border
        {
            Background = usuario
                ? new SolidColorBrush(Color.FromRgb(32, 25, 40))
                : Brushes.Transparent,
            CornerRadius = new CornerRadius(usuario ? 12 : 0),
            Padding = usuario ? new Thickness(16, 11, 16, 11) : new Thickness(0),
            Margin = new Thickness(0, 8, 0, 18),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MaxWidth = 820
        };

        border.Child = CriarConteudoMensagem(texto, carregando, !usuario);
        MessagesPanel.Children.Add(border);
        ChatScroll.ScrollToEnd();
        return border;
    }

    private UIElement CriarConteudoMensagem(string texto, bool carregando, bool resposta = false)
    {
        var painel = new StackPanel { Orientation = Orientation.Vertical };

        var cabecalho = new TextBlock
        {
            Text = resposta ? "KAYRON" : "Você",
            Foreground = resposta
                ? new SolidColorBrush(Color.FromRgb(199, 162, 255))
                : new SolidColorBrush(Color.FromRgb(169, 150, 184)),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 6)
        };

        var conteudoMensagem = CriarTextoComLinks(texto);

        painel.Children.Add(cabecalho);
        painel.Children.Add(conteudoMensagem);

        if (!carregando && resposta)
        {
            // As fontes são exibidas como ícones clicáveis no próprio texto.

            var copiar = new Button
            {
                Content = "Copiar",
                Padding = new Thickness(6, 3, 6, 3),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 7, 0, 0)
            };

            copiar.Click += (_, _) =>
            {
                Clipboard.SetText(texto ?? string.Empty);
                copiar.Content = "Copiado";
            };

            painel.Children.Add(copiar);
        }

        return painel;
    }

    private TextBlock CriarTextoComLinks(string texto)
    {
        var bloco = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(228, 212, 255)),
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            Cursor = Cursors.IBeam
        };

        var linhas = (texto ?? string.Empty)
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var emSecaoFontes = false;
        var contadorFontes = 0;

        for (var indice = 0; indice < linhas.Length; indice++)
        {
            var linha = linhas[indice];
            if (linha.Trim().Equals("**Fontes**", StringComparison.OrdinalIgnoreCase))
                emSecaoFontes = true;
            var linhaDeFonte = emSecaoFontes &&
                Regex.IsMatch(linha.Trim(), @"^-\s*\[.+\]\(https?://[^)]+\)$", RegexOptions.IgnoreCase);
            var padraoLinks =
                @"\*\*(?<negrito>.+?)\*\*|\[(?<rotulo>[^\]]+)\]\((?<markdownUrl>https?://[^)\s]+)\)|(?<url>https?://[^\s<>\]]+)";
            var ultimoIndice = 0;

            foreach (Match match in Regex.Matches(
                         linha,
                         padraoLinks,
                         RegexOptions.IgnoreCase))
            {
                if (match.Index > ultimoIndice &&
                    !(linhaDeFonte && string.IsNullOrWhiteSpace(linha[ultimoIndice..match.Index].Trim('-', ' '))))
                {
                    bloco.Inlines.Add(
                        new Run(linha[ultimoIndice..match.Index]));
                }

                if (match.Groups["negrito"].Success)
                {
                    var negrito = new Bold();
                    negrito.Inlines.Add(new Run(match.Groups["negrito"].Value));
                    bloco.Inlines.Add(negrito);
                }
                else
                {
                    var url = match.Groups["markdownUrl"].Success
                        ? match.Groups["markdownUrl"].Value
                        : match.Groups["url"].Value;

                    url = url.TrimEnd('.', ',', ';', ':', ')', ']', '}', '"');
                    var rotulo = match.Groups["rotulo"].Success
                        ? match.Groups["rotulo"].Value
                        : url;

                    if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                        (uri.Scheme == Uri.UriSchemeHttp ||
                         uri.Scheme == Uri.UriSchemeHttps))
                    {
                        var link = new Hyperlink
                        {
                            NavigateUri = uri,
                            TextDecorations = TextDecorations.Underline,
                            ToolTip = $"Abrir fonte: {url}"
                        };

                        if (linhaDeFonte)
                        {
                            contadorFontes++;
                            if (contadorFontes > 1)
                                bloco.Inlines.Add(new Run("  "));

                            var favicon = new Image
                            {
                                Width = 16,
                                Height = 16,
                                Margin = new Thickness(0, 0, 4, 0),
                                ToolTip = $"Abrir fonte: {uri.AbsoluteUri}",
                                Source = new BitmapImage(new Uri(
                                    $"https://www.google.com/s2/favicons?domain={uri.Host}&sz=16",
                                    UriKind.Absolute))
                            };

                            link.Inlines.Add(new InlineUIContainer(favicon)
                            {
                                BaselineAlignment = BaselineAlignment.Center
                            });
                            link.Inlines.Add(new Run($" {rotulo}"));
                        }
                        else
                        {
                            var favicon = new Image
                            {
                                Width = 16,
                                Height = 16,
                                Margin = new Thickness(0, 0, 4, 0),
                                ToolTip = $"Abrir fonte: {uri.AbsoluteUri}",
                                Source = new BitmapImage(new Uri(
                                    $"https://www.google.com/s2/favicons?domain={uri.Host}&sz=16",
                                    UriKind.Absolute))
                            };

                            link.Inlines.Add(new InlineUIContainer(favicon)
                            {
                                BaselineAlignment = BaselineAlignment.Center
                            });
                            link.Inlines.Add(new Run($" {rotulo}"));
                        }
                    link.Click += (_, _) =>
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = uri.AbsoluteUri,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(
                                $"Não foi possível abrir a fonte.\n\n{ex.Message}",
                                "KAYRON — Fonte",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        }
                    };

                    bloco.Inlines.Add(link);
                }
                    else
                    {
                        bloco.Inlines.Add(new Run(match.Value));
                    }
                }

                ultimoIndice = match.Index + match.Length;
            }

            if (ultimoIndice < linha.Length)
            {
                bloco.Inlines.Add(new Run(linha[ultimoIndice..]));
            }

            if (indice < linhas.Length - 1 && !linhaDeFonte)
                bloco.Inlines.Add(new LineBreak());
        }

        return bloco;
    }

    private async void ComandoRapido_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string comando && !string.IsNullOrWhiteSpace(comando))
        {
            InputBox.Text = comando;
            await EnviarMensagemAsync();
        }
    }

    private void NovaConversa_Click(object sender, RoutedEventArgs e)
    {
        _processamentoCts?.Cancel();
        _processamentoCts?.Dispose();
        _processamentoCts = null;
        _processando = false;

        App.Services.GetRequiredService<IHistoricoConversa>().Limpar();
        IniciarNovaSessao();
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e) => AtualizarLayout();

    private void AtualizarLayout()
    {
        var pequeno = ActualWidth < 820;
        var compacto = ActualWidth >= 820 && ActualWidth < 980;
        var larguraSidebar = pequeno ? 196 : compacto ? 68 : 260;

        SidebarColumn.Width = new GridLength(larguraSidebar);
        SidebarBorder.Visibility = Visibility.Visible;
        SidebarBorder.Width = larguraSidebar;

        ComposerPanel.Width = pequeno
            ? Math.Max(280, ActualWidth - 244)
            : compacto
                ? Math.Min(620, ActualWidth - 110)
                : Math.Min(680, ActualWidth - 320);

        NewChatText.Visibility = compacto ? Visibility.Collapsed : Visibility.Visible;
        SettingsText.Visibility = compacto ? Visibility.Collapsed : Visibility.Visible;
        BrandMark.Margin = compacto ? new Thickness(7, 0, 7, 0) : new Thickness(0, 0, 11, 0);
        SidebarHeader.Width = compacto ? 48 : double.NaN;

        MessagesPanel.Width = pequeno
            ? Math.Max(280, ActualWidth - 244)
            : compacto
                ? Math.Min(620, ActualWidth - 110)
                : Math.Min(680, ActualWidth - 320);

        MessagesPanel.MaxWidth = MessagesPanel.Width;
        TopBar.Margin = compacto ? new Thickness(14, 0, 14, 0) : new Thickness(18, 0, 18, 0);
        ConversationScroll.Visibility = Visibility.Visible;
        StatusPanel.Visibility = compacto ? Visibility.Collapsed : Visibility.Visible;
        ModelButton.Content = compacto || pequeno ? "IA ▾" : "IA / Plugins  ▾";
        NewChatButton.HorizontalContentAlignment = compacto ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        SettingsButton.HorizontalContentAlignment = compacto ? HorizontalAlignment.Center : HorizontalAlignment.Left;
    }

    private void Modelo_Click(object sender, RoutedEventArgs e)
    {
        var options = App.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<ModeloInteligenciaOptions>>().Value;
        var ferramentas = App.Services.GetServices<IFerramenta>()
            .Select(ferramenta => ferramenta.Nome)
            .OrderBy(nome => nome, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        var modelo = string.IsNullOrWhiteSpace(options.Modelo) ? "não informado" : options.Modelo;
        var provedor = string.IsNullOrWhiteSpace(options.Provedor) ? "não informado" : options.Provedor;
        MessageBox.Show(
            $"Provedor: {provedor}\nModelo: {modelo}\n\nFerramentas registradas ({ferramentas.Length}):\n{string.Join(", ", ferramentas)}\n\nPesquisa web: disponível pela ferramenta de internet; a execução depende do serviço externo e da rede.",
            "Integrações do KAYRON",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
