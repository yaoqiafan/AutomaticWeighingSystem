using AWS.UI.Views.BasicData;
using AWS.UI.Views.Settings;
using AWS.UI.Views.Statistics;
using AWS.UI.Views.Weighing;
using AWS.UI.ViewModels.BasicData;
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
        // 过磅操作
        containerRegistry.RegisterForNavigation<WeighingView, WeighingViewModel>(nameof(WeighingView));

        // 数据统计（单页：图表 + 档案表 + 导出）
        containerRegistry.RegisterForNavigation<StatisticsView, StatisticsViewModel>(nameof(StatisticsView));

        // 基础数据综合管理
        containerRegistry.RegisterForNavigation<BasicDataView, BasicDataViewModel>(nameof(BasicDataView));

        // 系统设置
        containerRegistry.RegisterForNavigation<ParameterManageView, ParameterManageViewModel>(nameof(ParameterManageView));
        containerRegistry.RegisterForNavigation<UserManageView, UserManageViewModel>(nameof(UserManageView));
        containerRegistry.RegisterForNavigation<CloudSyncView, CloudSyncViewModel>(nameof(CloudSyncView));
    }

    public void OnInitialized(IContainerProvider containerProvider) { }
}
