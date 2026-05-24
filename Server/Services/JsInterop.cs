using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Remotely.Server.Services;

public interface IJsInterop
{
    void AddBeforeUnloadHandler();

    void AddClassName(ElementReference element, string className);
    ValueTask Alert(string message);

    void AutoHeight();

    ValueTask<bool> Confirm(string message);

    ValueTask<int> GetCursorIndex(ElementReference terminalInput);

    void InvokeClick(string elementId);

    void OpenWindow(string url, string target);

    void PreventTabOut(ElementReference terminalInput);

    ValueTask<string> Prompt(string message);
    void ScrollToElement(ElementReference element);

    void ScrollToEnd(ElementReference element);

    ValueTask<bool> SetClipboardText(string text);

    void SetStyleProperty(ElementReference element, string propertyName, string value);
    void StartDraggingY(ElementReference element, double clientY);

    /// <summary>Read a value from browser <c>localStorage</c>. Returns null if the key is absent.</summary>
    ValueTask<string?> LocalStorageGetItem(string key);

    /// <summary>Write a value to browser <c>localStorage</c>.</summary>
    ValueTask LocalStorageSetItem(string key, string value);

    /// <summary>Remove a value from browser <c>localStorage</c>.</summary>
    ValueTask LocalStorageRemoveItem(string key);
}

public class JsInterop : IJsInterop
{
    private readonly IJSRuntime _jsRuntime;

    public JsInterop(IJSRuntime jSRuntime)
    {
        _jsRuntime = jSRuntime;
    }

    public void AddBeforeUnloadHandler()
    {
        _jsRuntime.InvokeVoidAsync("addBeforeUnloadHandler");
    }

    public void AddClassName(ElementReference element, string className)
    {
        _jsRuntime.InvokeVoidAsync("addClassName", element, className);
    }

    public ValueTask Alert(string message)
    {
        return _jsRuntime.InvokeVoidAsync("invokeAlert", message);
    }

    public void AutoHeight()
    {
        _jsRuntime.InvokeVoidAsync("autoHeight");
    }

    public ValueTask<bool> Confirm(string message)
    {
        return _jsRuntime.InvokeAsync<bool>("invokeConfirm", message);
    }

    public ValueTask<int> GetCursorIndex(ElementReference inputElement)
    {
        return _jsRuntime.InvokeAsync<int>("getSelectionStart", inputElement); 
    }

    public void InvokeClick(string elementId)
    {
        _jsRuntime.InvokeVoidAsync("invokeClick", elementId);
    }

    public void OpenWindow(string url, string target)
    {
        _jsRuntime.InvokeVoidAsync("openWindow", url, target);
    }

    public void PreventTabOut(ElementReference terminalInput)
    {
        _jsRuntime.InvokeVoidAsync("preventTabOut", terminalInput);
    }

    public ValueTask<string> Prompt(string message)
    {
        return _jsRuntime.InvokeAsync<string>("invokePrompt", message);
    }

    public void ScrollToElement(ElementReference element)
    {
        _jsRuntime.InvokeVoidAsync("scrollToElement", element);
    }

    public void ScrollToEnd(ElementReference element)
    {
        _jsRuntime.InvokeVoidAsync("scrollToEnd", element);
    }

    public async ValueTask<bool> SetClipboardText(string text)
    {
        return await _jsRuntime.InvokeAsync<bool>("setClipboardText", text);
    }

    public void SetStyleProperty(ElementReference element, string propertyName, string value)
    {
        _jsRuntime.InvokeVoidAsync("setStyleProperty", element, propertyName, value);
    }
    public void StartDraggingY(ElementReference element, double clientY)
    {
        _jsRuntime.InvokeVoidAsync("startDraggingY", element, clientY);
    }

    public async ValueTask<string?> LocalStorageGetItem(string key)
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", key);
        }
        catch
        {
            // localStorage may be unavailable (private mode, prerender, etc.). Treat as absent.
            return null;
        }
    }

    public async ValueTask LocalStorageSetItem(string key, string value)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, value);
        }
        catch
        {
            // Best-effort; ignore if storage is unavailable.
        }
    }

    public async ValueTask LocalStorageRemoveItem(string key)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
        }
        catch
        {
            // Best-effort.
        }
    }
}
