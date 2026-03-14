namespace GcaUpdater.Services;

public interface IUpdateUi
{
    void SetStatus(string message);
    void SetProgress(double value);
    void AppendLog(string message);
    void EnableCloseButton();
    void ShowError(string message);
    void ShowInfo(string message);
}
