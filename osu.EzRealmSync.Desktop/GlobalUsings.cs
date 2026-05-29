// WPF-UI 控件约定（包：WPF-UI + WPF-UI.Abstractions，已随 NuGet 引入 ControlsDictionary 主题）：
//
// 1) 独立类型 → 下列别名（C# 中写 Button / TextBox 即 Wpf.Ui.Controls.*）
// 2) ComboBox / CheckBox / RadioButton → 仍用 System.Windows.Controls 类型名，
//    但由 ui:ControlsDictionary 提供 Fluent 样式（XAML 不要写 ui: 前缀，也不要加 Std）
// 3) 仅当必须绕过 WPF-UI 时，使用 Win* 前缀（如 WinMessageBox）

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
global using UiMessageBox = Wpf.Ui.Controls.MessageBox;
global using UiMessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;
global using UiMessageBoxButton = Wpf.Ui.Controls.MessageBoxButton;
global using ControlAppearance = Wpf.Ui.Controls.ControlAppearance;
global using ContentDialogResult = Wpf.Ui.Controls.ContentDialogResult;

// —— 仅 Windows 基座（无 Fluent 替代）——
global using WinMessageBox = System.Windows.MessageBox;
global using WinMessageBoxButton = System.Windows.MessageBoxButton;
global using WinMessageBoxResult = System.Windows.MessageBoxResult;
global using WinMessageBoxImage = System.Windows.MessageBoxImage;
