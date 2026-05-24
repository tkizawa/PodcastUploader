using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PodcastUploader
{
    public class EpisodeInfo
    {
        public int EpisodeNumber { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string AudioFilePath { get; set; } = string.Empty;
    }

    public class MainForm : Form
    {
        private TextBox txtFilePath = null!;
        private Button btnBrowse = null!;
        private Button btnUpload = null!;
        private TextBox txtLog = null!;

        public MainForm()
        {
            InitializeComponent();
            LoadConfig();
        }

        private void InitializeComponent()
        {
            this.Text = "ポッドキャスト自動アップロード (Spotify向け)";
            this.ClientSize = new System.Drawing.Size(600, 420);
            this.StartPosition = FormStartPosition.CenterScreen;

            Label lblFile = new Label() { Text = "音声ファイル:", Left = 10, Top = 15, Width = 80 };
            txtFilePath = new TextBox() { Left = 100, Top = 12, Width = 380, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            btnBrowse = new Button() { Text = "参照...", Left = 490, Top = 10, Width = 90, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            btnUpload = new Button() { Text = "アップロード実行", Left = 10, Top = 50, Width = 570, Height = 40, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            txtLog = new TextBox() { Left = 10, Top = 100, Width = 570, Height = 300, Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };

            btnBrowse.Click += BtnBrowse_Click;
            btnUpload.Click += async (s, e) => await BtnUpload_Click(s, e);

            this.Controls.Add(lblFile);
            this.Controls.Add(txtFilePath);
            this.Controls.Add(btnBrowse);
            this.Controls.Add(btnUpload);
            this.Controls.Add(txtLog);
        }

        private string GetConfigPath()
        {
            var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);

            while (currentDirectory != null)
            {
                var projectFilePath = Path.Combine(currentDirectory.FullName, "PodcastUploader.csproj");
                if (File.Exists(projectFilePath))
                {
                    return Path.Combine(currentDirectory.FullName, "episode.json");
                }

                currentDirectory = currentDirectory.Parent;
            }

            return Path.Combine(AppContext.BaseDirectory, "episode.json");
        }

        private void LoadConfig()
        {
            string configPath = GetConfigPath();
            if (File.Exists(configPath))
            {
                try
                {
                    string jsonString = File.ReadAllText(configPath);
                    var episodeInfo = JsonSerializer.Deserialize<EpisodeInfo>(jsonString);
                    if (episodeInfo != null && !string.IsNullOrWhiteSpace(episodeInfo.AudioFilePath))
                    {
                        txtFilePath.Text = episodeInfo.AudioFilePath;
                    }
                }
                catch (Exception ex)
                {
                    Log($"設定ファイルの読み込みに失敗しました: {ex.Message}");
                }
            }
        }

        private void BtnBrowse_Click(object? sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "音声ファイル|*.mp3;*.wav;*.m4a;*.ogg|すべてのファイル|*.*";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    txtFilePath.Text = ofd.FileName;
                }
            }
        }

        private void Log(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => Log(message)));
                return;
            }
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n");
        }

        private async Task BtnUpload_Click(object? sender, EventArgs e)
        {
            string audioFilePath = txtFilePath.Text;
            if (!File.Exists(audioFilePath))
            {
                MessageBox.Show("音声ファイルが見つかりません。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string configPath = GetConfigPath();
            EpisodeInfo? episodeInfo = null;
            
            // Read Description from config if exists
            if (File.Exists(configPath))
            {
                try
                {
                    string jsonString = await File.ReadAllTextAsync(configPath);
                    episodeInfo = JsonSerializer.Deserialize<EpisodeInfo>(jsonString);
                }
                catch { }
            }

            if (episodeInfo == null)
            {
                episodeInfo = new EpisodeInfo();
            }

            // Update JSON and save it
            episodeInfo.AudioFilePath = audioFilePath;
            try
            {
                await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(episodeInfo, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                Log($"設定の保存に失敗しました: {ex.Message}");
            }

            btnUpload.Enabled = false;
            btnBrowse.Enabled = false;
            txtFilePath.Enabled = false;

            try
            {
                await RunUploadProcess(audioFilePath, episodeInfo);
            }
            catch (Exception ex)
            {
                Log($"エラー: {ex.Message}");
                MessageBox.Show($"エラーが発生しました:\n{ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnUpload.Enabled = true;
                btnBrowse.Enabled = true;
                txtFilePath.Enabled = true;
                Log("処理が完了しました。");
            }
        }

        private async Task RunUploadProcess(string audioFilePath, EpisodeInfo episodeInfo)
        {
            Log("MP3ファイルのタグ情報を解析中...");
            var tfile = TagLib.File.Create(audioFilePath);
            string episodeTitle = tfile.Tag.Title;

            if (string.IsNullOrWhiteSpace(episodeTitle))
            {
                throw new Exception("MP3ファイルのタイトル(タグ)が空、または設定されていません。");
            }

            var match = Regex.Match(episodeTitle, @"第(\d+)回");
            if (!match.Success)
            {
                throw new Exception($"MP3のタイトルに「第〇〇回」が含まれていません。\n読み取ったタイトル: {episodeTitle}");
            }

            string episodeNumberStr = match.Groups[1].Value;
            string description = episodeInfo.Description;
            string url = "https://creators.spotify.com/dash/show/22iMko5ns9X5DmwDgW4ut2/home";

            Log("Playwrightを初期化中...");
            using var playwright = await Playwright.CreateAsync();

            string userDataDir = Path.Combine(AppContext.BaseDirectory, "browser_data");

            Log("ブラウザを起動しています...");
            await using var context = await playwright.Chromium.LaunchPersistentContextAsync(userDataDir, new BrowserTypeLaunchPersistentContextOptions
            {
                Headless = false,
                SlowMo = 500
            });

            var page = context.Pages.Count > 0 ? context.Pages[0] : await context.NewPageAsync();

            Log("Spotify Creatorsのページを開いています...");
            await page.GotoAsync(url);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            Log("自動アップロード処理を開始します...");

            if (!File.Exists(audioFilePath))
            {
                await File.WriteAllBytesAsync(audioFilePath, new byte[1024]);
            }

            Log("「新しいエピソード」ボタンをクリックします...");
            var newEpisodeBtn = page.Locator("span:has-text('新しいエピソード'), span:has-text('New Episode')").First;
            await newEpisodeBtn.ClickAsync();

            Log($"{Path.GetFileName(audioFilePath)} をアップロードします...");
            var fileInput = page.Locator("input[type='file']").First;
            await fileInput.SetInputFilesAsync(audioFilePath);

            Log("アップロード画面の遷移を待機中...");
            await Task.Delay(3000);

            Log("タイトルを入力します...");
            var titleInput = page.GetByRole(AriaRole.Textbox, new() { NameRegex = new Regex("Title|タイトル", RegexOptions.IgnoreCase) }).First;
            await titleInput.FillAsync(episodeTitle);

            Log("説明文のHTML入力モードを有効にします...");
            var htmlToggle = page.Locator("label:has-text('HTML')").First;
            await htmlToggle.ClickAsync();
            await Task.Delay(500);

            Log("説明文を入力します...");
            var descInput = page.Locator("textarea[name='description'], div[role='textbox'][name='description']").First;
            await descInput.ClickAsync();
            await page.Keyboard.PressAsync("Control+A");
            await page.Keyboard.PressAsync("Backspace");

            try
            {
                await descInput.FillAsync(description);
            }
            catch
            {
                await page.Keyboard.InsertTextAsync(description);
            }

            Log("シーズン番号とエピソード番号を設定します...");
            var seasonInput = page.Locator("input[name='podcastSeasonNumber'], input#season-number").First;
            await seasonInput.FillAsync("1");

            var episodeInput = page.Locator("input[name='podcastEpisodeNumber'], input#episode-number").First;
            await episodeInput.FillAsync(episodeNumberStr);

            Log("「次へ」ボタンをクリックして公開設定画面へ進みます...");
            var nextBtn = page.Locator("button:has-text('次へ'), button:has-text('Next')").First;
            await nextBtn.ClickAsync();
            await Task.Delay(2000);

            Log("公開日「今すぐ」を選択します...");
            var publishNowRadio = page.Locator("label[for='publish-date-now']").First;
            await publishNowRadio.ClickAsync();

            Log("★すべての自動入力処理が完了しました！");
            Log("ブラウザで入力内容やファイルのアップロード状況を確認し、問題なければ手動で「公開」等のボタンを押して完了させてください。");
            
            Log("30秒後にブラウザは自動的に閉じられますが、その前に手動で完了させてください。");
            await Task.Delay(30000);
            
            MessageBox.Show("自動アップロード処理が完了しました。\n手動で確認を行い、「公開」ボタンを押してください。", "完了", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}
