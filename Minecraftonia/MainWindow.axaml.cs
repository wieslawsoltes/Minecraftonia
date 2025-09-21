using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Minecraftonia.Game;

namespace Minecraftonia;

public partial class MainWindow : Window
{
    private const string DefaultSaveSlot = "latest";

    private GameControl _gameControl = default!;
    private Border _mainMenuOverlay = default!;
    private Border _pauseMenuOverlay = default!;
    private TextBlock _mainMenuStatus = default!;
    private TextBlock _pauseMenuStatus = default!;
    private Button _loadButton = default!;

    public MainWindow()
    {
        InitializeComponent();

        _gameControl = this.FindControl<GameControl>("GameView") ?? throw new InvalidOperationException("Game view not found.");
        _mainMenuOverlay = this.FindControl<Border>("MainMenuOverlay") ?? throw new InvalidOperationException("Main menu overlay not found.");
        _pauseMenuOverlay = this.FindControl<Border>("PauseMenuOverlay") ?? throw new InvalidOperationException("Pause menu overlay not found.");
        _mainMenuStatus = this.FindControl<TextBlock>("MainMenuStatus") ?? throw new InvalidOperationException("Main menu status not found.");
        _pauseMenuStatus = this.FindControl<TextBlock>("PauseMenuStatus") ?? throw new InvalidOperationException("Pause menu status not found.");
        _loadButton = this.FindControl<Button>("LoadButton") ?? throw new InvalidOperationException("Load button not found.");

        _gameControl.PauseRequested += OnGamePauseRequested;

        ShowMainMenu();
        UpdateLoadButtonState();
    }

    private void OnGamePauseRequested(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(ShowPauseMenu);
    }

    private void StartButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _gameControl.StartNewGame();
        HideMenus();
        UpdateLoadButtonState();
    }

    private void LoadButton_OnClick(object? sender, RoutedEventArgs e)
    {
        string path = GetDefaultSavePath();
        try
        {
            var save = GameSaveService.Load(path);
            _gameControl.LoadGame(save);
            HideMenus();
        }
        catch (Exception ex)
        {
            SetStatus(_mainMenuStatus, $"Unable to load save: {ex.Message}");
            UpdateLoadButtonState();
        }
    }

    private void ExitButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ResumeButton_OnClick(object? sender, RoutedEventArgs e)
    {
        HideMenus();
        _gameControl.ResumeGame();
    }

    private void SaveAndExitButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (TrySaveGame(out string error))
        {
            Close();
        }
        else
        {
            SetStatus(_pauseMenuStatus, $"Save failed: {error}");
        }
    }

    private void ExitWithoutSavingButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BackToMenuButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ShowMainMenu();
        UpdateLoadButtonState();
    }

    private void ShowMainMenu(string? message = null)
    {
        _gameControl.PauseGame();
        _mainMenuOverlay.IsVisible = true;
        _pauseMenuOverlay.IsVisible = false;
        SetStatus(_pauseMenuStatus, null);
        SetStatus(_mainMenuStatus, message);
    }

    private void ShowPauseMenu()
    {
        _gameControl.PauseGame();
        _pauseMenuOverlay.IsVisible = true;
        _mainMenuOverlay.IsVisible = false;
        SetStatus(_pauseMenuStatus, null);
    }

    private void HideMenus()
    {
        _mainMenuOverlay.IsVisible = false;
        _pauseMenuOverlay.IsVisible = false;
        SetStatus(_mainMenuStatus, null);
        SetStatus(_pauseMenuStatus, null);
    }

    private bool TrySaveGame(out string error)
    {
        error = string.Empty;
        try
        {
            var save = _gameControl.CreateSaveData();
            GameSaveService.Save(save, GetDefaultSavePath());
            UpdateLoadButtonState();
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private void UpdateLoadButtonState()
    {
        if (_loadButton is null)
        {
            return;
        }

        string path = GetDefaultSavePath();
        _loadButton.IsEnabled = File.Exists(path);
    }

    private static void SetStatus(TextBlock target, string? message)
    {
        bool hasMessage = !string.IsNullOrWhiteSpace(message);
        target.IsVisible = hasMessage;
        target.Text = hasMessage ? message : string.Empty;
    }

    private static string GetDefaultSavePath()
    {
        return GameSaveService.GetSavePath(DefaultSaveSlot);
    }
}
