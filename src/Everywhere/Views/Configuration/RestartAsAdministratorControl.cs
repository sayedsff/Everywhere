using Avalonia.Controls;
using Avalonia.Layout;
using CommunityToolkit.Mvvm.Input;
using Everywhere.Interop;
using Lucide.Avalonia;
using Microsoft.Extensions.Logging;
using ShadUI;

namespace Everywhere.Views.Configuration;

public class RestartAsAdministratorControl : ContentControl
{
    public RestartAsAdministratorControl(INativeHelper nativeHelper, ToastManager toastManager, ILogger<RestartAsAdministratorControl> logger)
    {
        Content = new Button
        {
            [!ContentProperty] = new DynamicResourceKey(LocaleKey.Settings_Common_RestartAsAdministrator_Button_Content).ToBinding(),
            [ButtonAssist.IconSizeProperty] = 18,
            [ButtonAssist.IconProperty] = new LucideIcon
            {
                Kind = LucideIconKind.Shield
            },
            Classes = { "Outline" },
            HorizontalAlignment = HorizontalAlignment.Left,
            Command = new RelayCommand(() =>
            {
                try
                {
                    nativeHelper.RestartAsAdministrator();
                }
                catch (Exception ex)
                {
                    logger.LogInformation(ex, "Failed to restart as administrator.");
                    toastManager
                        .CreateToast(LocaleKey.Common_Error.I18N())
                        .WithContent(ex.GetFriendlyMessage())
                        .DismissOnClick()
                        .OnBottomRight()
                        .ShowError();
                }
            })
        };
    }
}