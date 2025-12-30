using FF14RisingstoneCheckIn.Models;
using FF14RisingstoneCheckIn.Services;
using FF14RisingstoneCheckIn.Utils;

namespace FF14RisingstoneCheckIn;

public partial class MainForm : Form
{
    private readonly Settings _settings;
    private readonly ApiClient _apiClient;
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _dailyTimer;
    private CancellationTokenSource? _loginCts;
    private bool _isLoggingIn;
    private bool _isSigningIn;
    private bool _startSilent;
    private bool _reallyClose;

    // 主题颜色
    private static readonly Color PrimaryColor = Color.FromArgb(55, 71, 79);      // BlueGrey 800
    private static readonly Color AccentColor = Color.FromArgb(3, 169, 244);      // Light Blue
    private static readonly Color SuccessColor = Color.FromArgb(76, 175, 80);     // Green
    private static readonly Color WarningColor = Color.FromArgb(255, 152, 0);     // Orange
    private static readonly Color ErrorColor = Color.FromArgb(244, 67, 54);       // Red
    private static readonly Color CardColor = Color.FromArgb(250, 250, 250);      // Light Grey
    private static readonly Color BorderColor = Color.FromArgb(224, 224, 224);    // Border

    public MainForm(bool startSilent = false)
    {
        _startSilent = startSilent;
        
        // 如果是静默启动，在初始化前设置窗口不可见
        if (startSilent)
        {
            WindowState = FormWindowState.Minimized;
            ShowInTaskbar = false;
        }
        
        InitializeComponent();

        _settings = Settings.Load();
        _apiClient = new ApiClient(_settings);

        // 设置事件处理
        _apiClient.OnLogMessage += OnLogMessage;
        _apiClient.OnStatusChanged += OnStatusChanged;
        _apiClient.OnCookieExpired += OnCookieExpired;
        _apiClient.OnLoginSuccess += OnLoginSuccess;

        // 初始化系统托盘
        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "FF14 石之家签到",
            Visible = true,
            ContextMenuStrip = CreateTrayMenu()
        };
        _notifyIcon.DoubleClick += (s, e) => ShowMainWindow();

        // 初始化每日定时器
        _dailyTimer = new System.Windows.Forms.Timer
        {
            Interval = 60000 // 每分钟检查一次
        };
        _dailyTimer.Tick += OnDailyTimerTick;

        // 加载UI状态
        LoadUIState();
    }

    private void InitializeComponent()
    {
        Text = "FF14 石之家自动签到";
        Size = new Size(460, 590);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        Icon = SystemIcons.Application;
        BackColor = Color.White;
        Font = new Font("Microsoft YaHei UI", 9F);

        var mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            BackColor = Color.White
        };

        int yPos = 20;

        // ========== 账号登录区域 ==========
        var loginCard = CreateCard(new Point(20, yPos), new Size(400, 120), "账号登录");
        
        var lblAccount = new Label
        {
            Text = "账号 (手机号/邮箱/盛趣账号)",
            Location = new Point(15, 32),
            AutoSize = true,
            ForeColor = Color.FromArgb(97, 97, 97),
            Font = new Font("Microsoft YaHei UI", 8F)
        };

        txtAccount = new TextBox
        {
            Location = new Point(15, 54),
            Size = new Size(280, 28),
            Font = new Font("Microsoft YaHei UI", 10F),
            BorderStyle = BorderStyle.FixedSingle
        };

        btnLogin = CreateButton("登录", new Point(305, 54), new Size(80, 32), true);
        btnLogin.Click += BtnLogin_Click;

        lblLoginStatus = new Label
        {
            Location = new Point(15, 90),
            AutoSize = true,
            ForeColor = AccentColor,
            Font = new Font("Microsoft YaHei UI", 8F)
        };

        loginCard.Controls.AddRange([lblAccount, txtAccount, btnLogin, lblLoginStatus]);
        yPos += 130;

        // ========== Cookie 状态区域 ==========
        var statusCard = CreateCard(new Point(20, yPos), new Size(400, 80), "状态");

        lblCookieStatus = new Label
        {
            Location = new Point(15, 35),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9F)
        };

        lblCookieTime = new Label
        {
            Location = new Point(15, 56),
            AutoSize = true,
            ForeColor = Color.Gray,
            Font = new Font("Microsoft YaHei UI", 8F)
        };

        statusCard.Controls.AddRange([lblCookieStatus, lblCookieTime]);
        yPos += 90;

        // ========== 设置区域 ==========
        var settingsCard = CreateCard(new Point(20, yPos), new Size(400, 70), "设置");

        chkAutoStart = new CheckBox
        {
            Text = "开机自启",
            Location = new Point(15, 38),
            AutoSize = true,
            ForeColor = Color.FromArgb(66, 66, 66),
            Cursor = Cursors.Hand
        };
        chkAutoStart.CheckedChanged += ChkAutoStart_CheckedChanged;

        chkAutoSignIn = new CheckBox
        {
            Text = "自动签到",
            Location = new Point(130, 38),
            AutoSize = true,
            ForeColor = Color.FromArgb(66, 66, 66),
            Cursor = Cursors.Hand
        };
        chkAutoSignIn.CheckedChanged += ChkAutoSignIn_CheckedChanged;

        settingsCard.Controls.AddRange([chkAutoStart, chkAutoSignIn]);
        yPos += 80;

        // ========== 签到操作区域 ==========
        var actionCard = CreateCard(new Point(20, yPos), new Size(400, 75), "签到");

        btnSignIn = CreateButton("立即签到", new Point(15, 35), new Size(100, 32), false);
        btnSignIn.Click += BtnSignIn_Click;

        lblSignInStatus = new Label
        {
            Location = new Point(125, 42),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9F)
        };

        lblLastSignIn = new Label
        {
            Location = new Point(260, 42),
            AutoSize = true,
            ForeColor = Color.Gray,
            Font = new Font("Microsoft YaHei UI", 8F)
        };

        actionCard.Controls.AddRange([btnSignIn, lblSignInStatus, lblLastSignIn]);
        yPos += 85;

        // ========== 日志区域 ==========
        var logCard = CreateCard(new Point(20, yPos), new Size(400, 120), "日志");

        txtLog = new TextBox
        {
            Location = new Point(10, 28),
            Size = new Size(380, 85),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 8.5F),
            BorderStyle = BorderStyle.None,
            BackColor = CardColor
        };

        logCard.Controls.Add(txtLog);

        // 添加所有卡片
        mainPanel.Controls.AddRange([loginCard, statusCard, settingsCard, actionCard, logCard]);
        Controls.Add(mainPanel);

        // 窗口事件
        FormClosing += MainForm_FormClosing;
        Load += MainForm_Load;
        Resize += MainForm_Resize;
    }

    private Panel CreateCard(Point location, Size size, string title)
    {
        var card = new Panel
        {
            Location = location,
            Size = size,
            BackColor = CardColor,
            BorderStyle = BorderStyle.None
        };
        card.Paint += (s, e) =>
        {
            using var pen = new Pen(BorderColor, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        var lblTitle = new Label
        {
            Text = title,
            Location = new Point(12, 8),
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold),
            ForeColor = PrimaryColor
        };
        card.Controls.Add(lblTitle);

        return card;
    }

    private Button CreateButton(string text, Point location, Size size, bool isAccent)
    {
        var btn = new Button
        {
            Text = text,
            Location = location,
            Size = size,
            FlatStyle = FlatStyle.Flat,
            BackColor = isAccent ? AccentColor : PrimaryColor,
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Font = new Font("Microsoft YaHei UI", 9F)
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = isAccent 
            ? Color.FromArgb(2, 136, 209) 
            : Color.FromArgb(69, 90, 100);
        return btn;
    }

    private TextBox txtAccount = null!;
    private Button btnLogin = null!;
    private Label lblLoginStatus = null!;
    private Label lblCookieStatus = null!;
    private Label lblCookieTime = null!;
    private CheckBox chkAutoSignIn = null!;
    private CheckBox chkAutoStart = null!;
    private Button btnSignIn = null!;
    private Label lblSignInStatus = null!;
    private Label lblLastSignIn = null!;
    private TextBox txtLog = null!;

    private ContextMenuStrip CreateTrayMenu()
    {
        var menu = new ContextMenuStrip();

        var showItem = new ToolStripMenuItem("显示主窗口");
        showItem.Click += (s, e) => ShowMainWindow();
        menu.Items.Add(showItem);

        var signInItem = new ToolStripMenuItem("立即签到");
        signInItem.Click += async (s, e) => await ExecuteSignIn();
        menu.Items.Add(signInItem);

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("退出");
        exitItem.Click += (s, e) =>
        {
            _reallyClose = true;
            Application.Exit();
        };
        menu.Items.Add(exitItem);

        return menu;
    }

    private void LoadUIState()
    {
        txtAccount.Text = _settings.Account;
        chkAutoSignIn.Checked = _settings.EnableAutoSignIn;
        chkAutoStart.Checked = AutoStartHelper.IsAutoStartEnabled();

        UpdateCookieStatusUI();
        UpdateLastSignInUI();
    }

    private void UpdateCookieStatusUI()
    {
        if (InvokeRequired)
        {
            Invoke(UpdateCookieStatusUI);
            return;
        }

        if (!string.IsNullOrWhiteSpace(_settings.SavedRisingstoneCookie))
        {
            lblCookieStatus.Text = "✓ Cookie 已保存";
            lblCookieStatus.ForeColor = SuccessColor;

            if (_settings.CookieSavedTime.HasValue)
                lblCookieTime.Text = $"保存于 {_settings.CookieSavedTime:yyyy-MM-dd HH:mm}";
            else
                lblCookieTime.Text = "";
        }
        else
        {
            lblCookieStatus.Text = "✗ 未保存 Cookie，请先登录";
            lblCookieStatus.ForeColor = WarningColor;
            lblCookieTime.Text = "";
        }
    }

    private void UpdateLastSignInUI()
    {
        if (InvokeRequired)
        {
            Invoke(UpdateLastSignInUI);
            return;
        }

        if (_settings.LastSignInTime.HasValue)
            lblLastSignIn.Text = $"上次 {_settings.LastSignInTime:MM-dd HH:mm}";
        else
            lblLastSignIn.Text = "";
    }

    private void ShowMainWindow()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void MinimizeToTray()
    {
        Hide();
        ShowInTaskbar = false;
    }

    #region Event Handlers

    private void MainForm_Load(object? sender, EventArgs e)
    {
        if (_startSilent)
        {
            MinimizeToTray();
        }

        // 启动时检查并签到
        _ = Task.Run(async () =>
        {
            await Task.Delay(2000); // 等待窗口完全加载
            await TryAutoSignIn();
        });

        // 启动定时器
        if (_settings.EnableAutoSignIn)
        {
            _dailyTimer.Start();
        }
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_reallyClose && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            MinimizeToTray();
            // 缩到托盘不发送通知
        }
        else
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _dailyTimer.Stop();
            _dailyTimer.Dispose();
            _apiClient.Dispose();
        }
    }

    private void MainForm_Resize(object? sender, EventArgs e)
    {
        if (WindowState == FormWindowState.Minimized)
        {
            MinimizeToTray();
        }
    }

    private void OnDailyTimerTick(object? sender, EventArgs e)
    {
        if (DateTimeHelper.ShouldSignInToday(_settings.LastSignInTime))
        {
            _ = TryAutoSignIn();
        }
    }

    private async void BtnLogin_Click(object? sender, EventArgs e)
    {
        if (_isLoggingIn)
        {
            AppendLog("正在登录中，请稍候...");
            return;
        }

        var account = txtAccount.Text.Trim();
        if (string.IsNullOrWhiteSpace(account))
        {
            MessageBox.Show("请输入账号", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _settings.Account = account;
        _settings.Save();

        _isLoggingIn = true;
        btnLogin.Enabled = false;
        lblLoginStatus.Text = "登录中...";
        lblLoginStatus.ForeColor = AccentColor;

        try
        {
            _loginCts = new CancellationTokenSource();
            var success = await _apiClient.ExecutePushLoginAsync(account);

            if (success)
            {
                lblLoginStatus.Text = "登录成功！";
                lblLoginStatus.ForeColor = SuccessColor;
                UpdateCookieStatusUI();

                // 登录成功后自动签到
                await ExecuteSignIn();
            }
        }
        finally
        {
            _isLoggingIn = false;
            btnLogin.Enabled = true;
            _loginCts?.Dispose();
            _loginCts = null;
        }
    }

    private async void BtnSignIn_Click(object? sender, EventArgs e)
    {
        await ExecuteSignIn();
    }

    private void ChkAutoSignIn_CheckedChanged(object? sender, EventArgs e)
    {
        _settings.EnableAutoSignIn = chkAutoSignIn.Checked;
        _settings.Save();

        if (_settings.EnableAutoSignIn)
            _dailyTimer.Start();
        else
            _dailyTimer.Stop();
    }

    private void ChkAutoStart_CheckedChanged(object? sender, EventArgs e)
    {
        var success = AutoStartHelper.SetAutoStart(chkAutoStart.Checked);
        if (!success)
        {
            MessageBox.Show("设置开机自启动失败", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            chkAutoStart.Checked = !chkAutoStart.Checked;
        }
        else
        {
            _settings.EnableAutoStart = chkAutoStart.Checked;
            _settings.Save();
        }
    }

    #endregion

    #region API Events

    private void OnLogMessage(string message)
    {
        AppendLog(message);
    }

    private void OnStatusChanged(string status)
    {
        if (InvokeRequired)
        {
            Invoke(() => OnStatusChanged(status));
            return;
        }

        lblLoginStatus.Text = status;
    }

    private void OnCookieExpired()
    {
        if (InvokeRequired)
        {
            Invoke(OnCookieExpired);
            return;
        }

        AppendLog("Cookie 已过期，需要重新登录");
        UpdateCookieStatusUI();

        // Cookie 过期时发送通知
        ShowToast("Cookie 已过期", "请重新登录以继续自动签到");
        ShowMainWindow();
    }

    private void OnLoginSuccess()
    {
        if (InvokeRequired)
        {
            Invoke(OnLoginSuccess);
            return;
        }

        UpdateCookieStatusUI();
    }

    #endregion

    #region Sign In Methods

    private async Task TryAutoSignIn()
    {
        if (string.IsNullOrWhiteSpace(_settings.SavedRisingstoneCookie))
        {
            AppendLog("未保存 Cookie，跳过自动签到");
            
            // Cookie 不存在时发送通知
            ShowToast("需要登录", "请登录以启用自动签到功能");
            
            // 在 UI 线程中显示主窗口
            if (InvokeRequired)
                Invoke(ShowMainWindow);
            else
                ShowMainWindow();
            
            return;
        }

        if (!DateTimeHelper.ShouldSignInToday(_settings.LastSignInTime))
        {
            AppendLog("今日已签到，跳过");
            return;
        }

        AppendLog("开始自动签到...");
        await _apiClient.ValidateAndSignInWithSavedCookieAsync();
        UpdateLastSignInUI();
    }

    private async Task ExecuteSignIn()
    {
        if (_isSigningIn)
        {
            AppendLog("正在签到中，请稍候...");
            return;
        }

        _isSigningIn = true;
        
        if (InvokeRequired)
        {
            Invoke(() =>
            {
                btnSignIn.Enabled = false;
                lblSignInStatus.Text = "签到中...";
            });
        }
        else
        {
            btnSignIn.Enabled = false;
            lblSignInStatus.Text = "签到中...";
        }

        try
        {
            var (success, message) = await _apiClient.ExecuteSignInAndClaimRewardsAsync();

            if (InvokeRequired)
            {
                Invoke(() =>
                {
                    lblSignInStatus.Text = success ? "✓ 完成" : "✗ 失败";
                    lblSignInStatus.ForeColor = success ? SuccessColor : ErrorColor;
                });
            }
            else
            {
                lblSignInStatus.Text = success ? "✓ 完成" : "✗ 失败";
                lblSignInStatus.ForeColor = success ? SuccessColor : ErrorColor;
            }

            UpdateLastSignInUI();

            // 签到结果时发送通知（无论成功或失败）
            ShowToast(success ? "签到成功" : "签到失败", message);
        }
        finally
        {
            _isSigningIn = false;
            
            if (InvokeRequired)
                Invoke(() => btnSignIn.Enabled = true);
            else
                btnSignIn.Enabled = true;
        }
    }

    #endregion

    #region Helpers

    private void AppendLog(string message)
    {
        if (InvokeRequired)
        {
            Invoke(() => AppendLog(message));
            return;
        }

        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        txtLog.AppendText($"[{timestamp}] {message}{Environment.NewLine}");
        txtLog.SelectionStart = txtLog.Text.Length;
        txtLog.ScrollToCaret();
    }

    private void ShowToast(string title, string message)
    {
        try
        {
            // 使用 Windows 气泡通知
            _notifyIcon.BalloonTipTitle = title;
            _notifyIcon.BalloonTipText = message;
            _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
            _notifyIcon.ShowBalloonTip(5000);
        }
        catch
        {
            // 忽略通知错误
        }
    }

    #endregion
}
