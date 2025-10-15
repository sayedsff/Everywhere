using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Everywhere.AI;
using Everywhere.Common;
using Everywhere.Configuration;
using Lucide.Avalonia;
using Serilog;
using ShadUI;

namespace Everywhere.ViewModels;

public partial class CustomAssistantPageViewModel(IKernelMixinFactory kernelMixinFactory, Settings settings) : ReactiveViewModelBase
{
    public ObservableCollection<CustomAssistant> CustomAssistants => settings.Model.CustomAssistants;

    [ObservableProperty]
    public partial CustomAssistant? SelectedCustomAssistant { get; set; }

    private static Color[] RandomAssistantIconBackgrounds { get; } =
    [
        Colors.MediumPurple,
        Colors.CadetBlue,
        Colors.Coral,
        Colors.CornflowerBlue,
        Colors.DarkCyan,
        Colors.DarkGoldenrod,
        Colors.DarkKhaki,
        Colors.DarkOrange,
        Colors.DarkSalmon,
        Colors.DarkSeaGreen,
        Colors.DarkTurquoise,
        Colors.DeepSkyBlue,
        Colors.DodgerBlue,
        Colors.ForestGreen,
        Colors.Goldenrod,
        Colors.IndianRed,
        Colors.LightCoral,
        Colors.LightSeaGreen,
        Colors.MediumSeaGreen,
        Colors.MediumSlateBlue,
        Colors.MediumTurquoise,
        Colors.OliveDrab,
        Colors.OrangeRed,
        Colors.RoyalBlue,
        Colors.SeaGreen,
        Colors.SteelBlue,
    ];

    [RelayCommand]
    private void CreateNewCustomAssistant()
    {
        var newAssistant = new CustomAssistant
        {
            Name = LocaleKey.CustomAssistant_Name_Default.I18N(),
            Icon = new ColoredIcon(
                ColoredIconType.Lucide,
                background: RandomAssistantIconBackgrounds[Random.Shared.Next(RandomAssistantIconBackgrounds.Length)])
            {
                Kind = LucideIconKind.Bot
            }
        };
        settings.Model.CustomAssistants.Add(newAssistant);
        SelectedCustomAssistant = newAssistant;
    }

    [RelayCommand]
    private async Task CheckConnectivityAsync(CancellationToken cancellationToken)
    {
        if (SelectedCustomAssistant is not { } customAssistant) return;

        try
        {
            await kernelMixinFactory.GetOrCreate(customAssistant).CheckConnectivityAsync(cancellationToken);
            ToastManager
                .CreateToast(LocaleKey.CustomAssistantPageViewModel_CheckConnectivity_SuccessToast_Title.I18N())
                .DismissOnClick()
                .ShowSuccess();
        }
        catch (Exception ex)
        {
            ex = HandledChatException.Handle(ex);
            Log.Logger.ForContext<CustomAssistantPageViewModel>().Error(
                ex,
                "Failed to check connectivity key for endpoint {ProviderId} and model {ModelId}",
                customAssistant.Endpoint.ActualValue,
                customAssistant.ModelId);
            ToastManager
                .CreateToast(LocaleKey.CustomAssistantPageViewModel_CheckConnectivity_FailedToast_Title.I18N())
                .WithContent(ex.GetFriendlyMessage().ToTextBlock())
                .DismissOnClick()
                .ShowError();
        }
    }

    [RelayCommand]
    private async Task DeleteCustomAssistantAsync()
    {
        if (SelectedCustomAssistant is not { } customAssistant) return;
        var dialogTcs = new TaskCompletionSource<bool>();
        DialogManager.CreateDialog(
                LocaleKey.Common_Warning.I18N(),
                LocaleKey.CustomAssistantPageViewModel_DeleteCustomAssistant_Dialog_Message.I18N(new DirectResourceKey(customAssistant.Name)))
            .WithPrimaryButton(LocaleKey.Common_Yes.I18N(), () => dialogTcs.SetResult(true))
            .WithCancelButton(LocaleKey.Common_No.I18N(), () => dialogTcs.SetResult(false))
            .Show();
        if (!await dialogTcs.Task) return;

        settings.Model.CustomAssistants.Remove(customAssistant);
    }
}