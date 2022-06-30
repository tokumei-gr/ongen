using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ongen
{
    public static class TaskExtensions
    {
        public static void Repeat(this Task taskToRepeat, CancellationToken cancellationToken, TimeSpan intervalTimeSpan)
        {
            var action = taskToRepeat
                .GetType()
                .GetField("m_action", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(taskToRepeat) as Action;

            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    if (cancellationToken.WaitHandle.WaitOne(intervalTimeSpan))
                        break;
                    if (cancellationToken.IsCancellationRequested)
                        break;
                    Task.Factory.StartNew(action, cancellationToken);
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Recurrent Cancellable Task
        /// </summary>
        public static class RecurrentCancellableTask
        {
            /// <summary>
            /// Starts a new task in a recurrent manner repeating it according to the polling interval.
            /// Whoever use this method should protect himself by surrounding critical code in the task 
            /// in a Try-Catch block.
            /// </summary>
            /// <param name="action">The action.</param>
            /// <param name="pollInterval">The poll interval.</param>
            /// <param name="token">The token.</param>
            /// <param name="taskCreationOptions">The task creation options</param>
            public static void StartNew(Action action,
                TimeSpan pollInterval,
                CancellationToken token,
                TaskCreationOptions taskCreationOptions = TaskCreationOptions.None)
            {
                Task.Factory.StartNew(
                    () =>
                    {
                        do
                        {
                            try
                            {
                                action();
                                if (token.WaitHandle.WaitOne(pollInterval)) break;
                            }
                            catch
                            {
                                return;
                            }
                        }
                        while (true);
                    },
                    token,
                    taskCreationOptions,
                    TaskScheduler.Default);
            }
        }
    }
}
