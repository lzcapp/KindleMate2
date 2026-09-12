using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace KindleMate2.Avalonia.Collections;

/// <summary>
/// 支持「整批替换、只发一次通知」的 <see cref="ObservableCollection{T}"/>。
///
/// 存在的理由：主列表动辄数千条(实测库 5,843 条)，而重载/筛选时要整体换掉内容。
/// 若用 <c>Clear()</c> + 逐条 <c>Add()</c>，每个元素都会触发一次 CollectionChanged，
/// 绑定控件随之各做一次更新 —— 在 UI 线程上就是上万次通知，窗口直接卡死，
/// 连进度条的跑马灯动画都会停住(用户观感:「导入很久，也没有进度」)。
///
/// <see cref="ReplaceAll"/> 直接改写底层列表，然后只发一次 Reset 通知，把开销从 O(n) 次
/// 界面更新降到 1 次。集合实例保持不变，因此既有绑定与选中项语义不受影响。
/// </summary>
public sealed class BulkObservableCollection<T> : ObservableCollection<T> {
    public BulkObservableCollection() { }

    public BulkObservableCollection(IEnumerable<T> items) : base(items) { }

    /// <summary>整批替换内容，只触发一次 Reset 通知。</summary>
    public void ReplaceAll(IEnumerable<T> items) {
        // 直接操作受保护的 Items(Collection<T> 的底层 List)，跳过逐条通知
        Items.Clear();
        foreach (var item in items) {
            Items.Add(item);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
