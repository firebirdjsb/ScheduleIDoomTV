using UnityEngine;

namespace S1API.TVApp;

public abstract class TVApp
{
    protected abstract string AppName { get; }
    protected abstract string AppTitle { get; }
    protected abstract Sprite Icon { get; }

    protected abstract void OnCreatedUI(GameObject container);
    protected virtual void OnUpdate() { }
    protected virtual void OnOpened() { }
    protected virtual void OnClosed() { }
    protected virtual void OnPaused() { }
    protected virtual void OnResumed() { }

    public bool IsOpen => false;
    public bool IsPaused => false;
    public void Open() { }
    public void Close() { }
    public void Pause() { }
    public void Resume() { }
}
