using System.Diagnostics;
using Microsoft.Build.Framework;

static class TaskItemExtensions
{
    [DebuggerStepThrough]
    public static string FullPath(this ITaskItem item)
    {
        return item.GetMetadata("FullPath");
    }

    [DebuggerStepThrough]
    public static string RootDir(this ITaskItem item)
    {
        return item.GetMetadata("RootDir");
    }

    [DebuggerStepThrough]
    public static string Filename(this ITaskItem item)
    {
        return item.GetMetadata("Filename");
    }

    [DebuggerStepThrough]
    public static string Extension(this ITaskItem item)
    {
        return item.GetMetadata("Extension");
    }

    [DebuggerStepThrough]
    public static string Directory(this ITaskItem item)
    {
        return item.GetMetadata("Directory");
    }
}