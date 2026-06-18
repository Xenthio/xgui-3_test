// Minimal Sandbox/FakeDesktop stubs for standalone testing
// This replaces all S&box-specific APIs with no-ops or simple equivalents

// Make IInstructionHandler globally visible (S&box exposes X86 namespace globally)
global using FakeOperatingSystem.Experiments.Ambitious.X86;
// Expose Sandbox namespace globally (S&box injects it as global)
global using Sandbox;
// GameTask stub — real class because S&box adds RunInThreadAsync/MainThread extension-like statics
global using GameTask = StandaloneStubs.GameTaskStub;

using System;
using System.Threading.Tasks;
using System.Collections.Generic;

// ── Sandbox namespace stubs ──────────────────────────────────────────────────

namespace Sandbox
{
    public static class Log
    {
        public static bool Silent = false;
        public static void Info(object msg)    { if (!Silent) Console.WriteLine("[INFO]  " + msg); }
        public static void Warning(object msg) { if (!Silent) Console.WriteLine("[WARN]  " + msg); }
        public static void Error(object msg)   { if (!Silent) Console.WriteLine("[ERROR] " + msg); }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class ConVarAttribute : Attribute
    {
        public ConVarAttribute(string name) { }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class ConCmdAttribute : Attribute
    {
        public ConCmdAttribute(string name) { }
    }

    public class SyncTask { }

    namespace Tasks { }
}

namespace Sandbox.Internal
{
    public static class GlobalGameNamespace { }
    public static class GlobalToolsNamespace { }
}

// ── Sandbox.Game stub ──────────────────────────────────────────────────────────────

namespace Sandbox
{
    public class Scene
    {
        public T GetSystem<T>() where T : class, new() => new T();
    }

    public static class Game
    {
        public static Scene ActiveScene { get; } = new Scene();

        public static class RootPanel
        {
            public static void AddChild(Sandbox.UI.Panel p) { }
        }
    }
}

// ── XGUISystem / AboutDialog stubs ──────────────────────────────────────────────────

public class XGUISystem
{
    public static XGUISystem Instance { get; } = new XGUISystem();
    public Sandbox.UI.Panel Panel { get; } = new Sandbox.UI.Panel();
}

public class AboutDialog : XGUI.Window
{
    public string AppName { get; set; }
    public string Message { get; set; }
    public string Body { get; set; }
}

// ── FakeDesktop stubs ────────────────────────────────────────────────────────

namespace FakeDesktop
{
    public enum MessageBoxIcon { None, Information, Warning, Error, Question }
    public enum MessageBoxButtons { OK, OKCancel, YesNo, YesNoCancel, AbortRetryIgnore, RetryCancel }
    public enum MessageBoxResult { None, OK, Cancel, Yes, No, Abort, Retry, Ignore }

    public static class MessageBoxUtility
    {
        public static Task<MessageBoxResult> ShowBlocking(string msg, string title,
            MessageBoxIcon icon = MessageBoxIcon.Error,
            MessageBoxButtons buttons = MessageBoxButtons.OK)
        {
            Console.WriteLine($"[MSGBOX] {title}: {msg}");
            return Task.FromResult(MessageBoxResult.Ignore);
        }

        public static void ShowCustom(string msg, string title,
            MessageBoxIcon icon = MessageBoxIcon.Error,
            MessageBoxButtons buttons = MessageBoxButtons.OK)
        {
            Console.WriteLine($"[MSGBOX] {title}: {msg}");
        }
    }
}

// ── Win32.User32 / XGUI stubs ────────────────────────────────────────────────

namespace Win32.User32 { }
namespace XGUI { }

namespace FakeDesktop.Controls { }

// ── FakeOperatingSystem.Registry stub (no-op for standalone tests) ───────────

namespace FakeOperatingSystem
{
    public class Registry
    {
        public static Registry Instance { get; } = null; // Always null in tests — Advapi32 no-ops

        public bool KeyExists(string path) => false;
        public void SetValue(string path, string name, object value) { }
        public void DeleteValue(string path, string name) { }
        public System.Collections.Generic.IReadOnlyDictionary<string, object> GetValues(string path) => null;
        public T GetValue<T>(string path, string name, T def = default) => def;
    }
}

// ── GameTask stub (no-op for standalone tests) ───────────────────────────────

namespace StandaloneStubs
{
    public static class GameTaskStub
    {
        // Mirrors GameTask.RunInThreadAsync — just runs on thread pool in tests
        public static System.Threading.Tasks.Task RunInThreadAsync( System.Func<System.Threading.Tasks.Task> fn )
            => System.Threading.Tasks.Task.Run( fn );

        public static System.Threading.Tasks.Task RunInThreadAsync( System.Func<System.Threading.Tasks.Task<System.Object>> fn )
            => System.Threading.Tasks.Task.Run( fn );

        // Mirrors GameTask.MainThread() — no-op in tests (already on a thread pool thread)
        public static System.Threading.Tasks.Task MainThread()
            => System.Threading.Tasks.Task.CompletedTask;

        // Mirrors GameTask.WaitAll
        public static void WaitAll( params System.Threading.Tasks.Task[] tasks )
            => System.Threading.Tasks.Task.WaitAll( tasks );
    }
}
