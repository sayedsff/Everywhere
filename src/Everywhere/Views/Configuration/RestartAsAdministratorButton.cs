using Avalonia.Controls;
using Avalonia.Layout;
using CommunityToolkit.Mvvm.Input;
using Everywhere.Interop;
using Lucide.Avalonia;
using Microsoft.Extensions.Logging;
using ShadUI;

namespace Everywhere.Views.Configuration;

public class RestartAsAdministratorButton : Button
{
    public RestartAsAdministratorButton(INativeHelper nativeHelper, ToastManager toastManager, ILogger<RestartAsAdministratorButton> logger)
    {
        this[!ContentProperty] = new DynamicResourceKey(LocaleKey.Settings_Common_RestartAsAdministrator_Button_Content).ToBinding();
        this[ButtonAssist.IconProperty] = new LucideIcon
        {
            Kind = LucideIconKind.Shield,
            Size = 18,
            Width = 18,
            Height = 18,
            Margin = new Thickness(0, 0, 6, 0)
        };
        Classes.Add("Outline");
        HorizontalAlignment = HorizontalAlignment.Left;
        Command = new RelayCommand(() =>
        {
            try
            {
                nativeHelper.RestartAsAdministrator();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to restart as administrator.");
                toastManager
                    .CreateToast(LocaleKey.Common_Error.I18N())
                    .WithContent(ex.GetFriendlyMessage())
                    .DismissOnClick()
                    .OnBottomRight()
                    .ShowError();
            }
        });
    }
}