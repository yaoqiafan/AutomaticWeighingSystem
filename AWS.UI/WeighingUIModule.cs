using AWS.UI.Views.BasicData;
using AWS.UI.Views.Records;
using AWS.UI.Views.Settings;
using AWS.UI.Views.Statistics;
using AWS.UI.Views.Weighing;
using AWS.UI.ViewModels.BasicData;
using AWS.UI.ViewModels.Records;
using AWS.UI.ViewModels.Settings;
using AWS.UI.ViewModels.Statistics;
using AWS.UI.ViewModels.Weighing;
using Prism.Ioc;
using Prism.Modularity;

namespace AWS.UI;

public class WeighingUIModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterForNavigation<WeighingView, WeighingViewModel>(nameof(WeighingView));
        containerRegistry.RegisterForNavigation<QueueView, QueueViewModel>(nameof(QueueView));
        containerRegistry.RegisterForNavigation<ArchiveQueryView, ArchiveQueryViewModel>(nameof(ArchiveQueryView));
        containerRegistry.RegisterForNavigation<ExportView, ExportViewModel>(nameof(ExportView));
        containerRegistry.RegisterForNavigation<StatisticsView, StatisticsViewModel>(nameof(StatisticsView));
        containerRegistry.RegisterForNavigation<CustomerView, CustomerViewModel>(nameof(CustomerView));
        containerRegistry.RegisterForNavigation<GoodsCategoryView, GoodsCategoryViewModel>(nameof(GoodsCategoryView));
        containerRegistry.RegisterForNavigation<VehicleView, VehicleViewModel>(nameof(VehicleView));
        containerRegistry.RegisterForNavigation<SerialPortSettingView, SerialPortSettingViewModel>(nameof(SerialPortSettingView));
        containerRegistry.RegisterForNavigation<UserManageView, UserManageViewModel>(nameof(UserManageView));
        containerRegistry.RegisterForNavigation<CloudSyncView, CloudSyncViewModel>(nameof(CloudSyncView));
    }

    public void OnInitialized(IContainerProvider containerProvider) { }
}
