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

        // 今日汇总（数据看板）
        containerRegistry.RegisterForNavigation<DashboardView, DashboardViewModel>(nameof(DashboardView));

        // 历史查询（档案明细 + 导出 + 图表分析）
        containerRegistry.RegisterForNavigation<HistoryView, HistoryViewModel>(nameof(HistoryView));

        // 基础数据综合管理
        containerRegistry.RegisterForNavigation<BasicDataView, BasicDataViewModel>(nameof(BasicDataView));

        // 系统设置
        containerRegistry.RegisterForNavigation<ParameterManageView, ParameterManageViewModel>(nameof(ParameterManageView));
        containerRegistry.RegisterForNavigation<UserManageView, UserManageViewModel>(nameof(UserManageView));
        containerRegistry.RegisterForNavigation<CloudSyncView, CloudSyncViewModel>(nameof(CloudSyncView));
    }

    public void OnInitialized(IContainerProvider containerProvider) { }
}
