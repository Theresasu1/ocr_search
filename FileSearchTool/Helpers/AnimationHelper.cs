using System;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace FileSearchTool.Helpers
{
    /// <summary>
    /// 动画辅助类，用于实现动态#号效果
    /// </summary>
    public class AnimationHelper : IDisposable
    {
        private readonly DispatcherTimer _timer;
        private int _hashCount = 0;
        private int _maxHashCount = 5;
        private readonly Action<string> _updateStatusAction;
        private string _baseStatusText = string.Empty;
        private bool _isRunning = false;

        public AnimationHelper(Action<string> updateStatusAction, int maxHashCount = 5)
        {
            _updateStatusAction = updateStatusAction;
            _maxHashCount = maxHashCount;
            _baseStatusText = string.Empty;
            
            // 创建定时器，每500毫秒更新一次
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _timer.Tick += new EventHandler(Timer_Tick);
        }

        /// <summary>
        /// 开始动画
        /// </summary>
        /// <param name="baseStatusText">基础状态文本</param>
        public void Start(string baseStatusText)
        {
            _baseStatusText = baseStatusText;
            _hashCount = 0;
            _isRunning = true;
            _timer.Start();
        }

        /// <summary>
        /// 停止动画
        /// </summary>
        public void Stop()
        {
            _isRunning = false;
            _timer.Stop();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (!_isRunning)
                return;

            // 增加#号数量，循环显示
            _hashCount = (_hashCount + 1) % (_maxHashCount + 1);
            
            // 如果是0，则显示最大数量的#号
            int displayCount = _hashCount == 0 ? _maxHashCount : _hashCount;
            
            // 构建新的状态文本
            string newStatusText = $"{_baseStatusText}{new string('#', displayCount)}";
            
            // 更新状态
            _updateStatusAction(newStatusText);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Stop();
            _timer.Tick -= Timer_Tick;
            // DispatcherTimer没有Dispose方法
        }
    }
}