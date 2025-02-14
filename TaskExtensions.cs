using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;

namespace SolutionMapper
{
    public static class TaskExtensions
    {
        public static void FireAndForget(this Task task)
        {
            task.Forget(); // Using VS Threading library's built-in method
        }
    }
}