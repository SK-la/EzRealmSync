// WPF-UI 控件约定（WPF-UI 4.3 + ControlsDictionary 主题）：
//
// 1) 独立类型 → 下列别名（C# 中写 Button / TextBox 即 Wpf.Ui.Controls.*）
// 2) ComboBox / CheckBox / RadioButton → System.Windows.Controls，由 ControlsDictionary 提供 Fluent 样式
// 3) 对话框 / 通知 → WpfUiServices（ContentDialogHost + SnackbarPresenter）

global using System.Windows;
global using System.Windows.Controls;
global using System.Windows.Input;
global using Wpf.Ui;
global using Wpf.Ui.Appearance;
global using Wpf.Ui.Controls;
global using Wpf.Ui.Extensions;

// —— Wpf.Ui.Controls 独立控件 ——
global using Button = Wpf.Ui.Controls.Button;
global using TextBox = Wpf.Ui.Controls.TextBox;
global using TextBlock = Wpf.Ui.Controls.TextBlock;
global using DataGrid = Wpf.Ui.Controls.DataGrid;
global using ListView = Wpf.Ui.Controls.ListView;
global using Card = Wpf.Ui.Controls.Card;
global using FluentWindow = Wpf.Ui.Controls.FluentWindow;
global using TitleBar = Wpf.Ui.Controls.TitleBar;
global using ContentDialog = Wpf.Ui.Controls.ContentDialog;
global using ContentDialogHost = Wpf.Ui.Controls.ContentDialogHost;
global using DropDownButton = Wpf.Ui.Controls.DropDownButton;
global using ProgressRing = Wpf.Ui.Controls.ProgressRing;
global using Snackbar = Wpf.Ui.Controls.Snackbar;
global using SnackbarPresenter = Wpf.Ui.Controls.SnackbarPresenter;
global using ToggleSwitch = Wpf.Ui.Controls.ToggleSwitch;
global using BreadcrumbBar = Wpf.Ui.Controls.BreadcrumbBar;
global using BreadcrumbBarItem = Wpf.Ui.Controls.BreadcrumbBarItem;
global using UiMessageBox = Wpf.Ui.Controls.MessageBox;
global using UiMessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;
global using ControlAppearance = Wpf.Ui.Controls.ControlAppearance;
global using ContentDialogResult = Wpf.Ui.Controls.ContentDialogResult;
global using ContentDialogButton = Wpf.Ui.Controls.ContentDialogButton;

// —— Wpf.Ui 服务 ——
global using IContentDialogService = Wpf.Ui.IContentDialogService;
global using ContentDialogService = Wpf.Ui.ContentDialogService;
global using ISnackbarService = Wpf.Ui.ISnackbarService;
global using SnackbarService = Wpf.Ui.SnackbarService;
global using SimpleContentDialogCreateOptions = Wpf.Ui.SimpleContentDialogCreateOptions;
