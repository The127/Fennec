using System.Runtime.InteropServices.JavaScript;

public static partial class LocalStorageInterop
{
    [JSImport("window.myLocalStorage.setItem", "")]
    public static partial void SetItem(string key, string value);

    [JSImport("window.myLocalStorage.getItem", "")]
    public static partial string? GetItem(string key);

    [JSImport("window.myLocalStorage.removeItem", "")]
    public static partial void RemoveItem(string key);
}