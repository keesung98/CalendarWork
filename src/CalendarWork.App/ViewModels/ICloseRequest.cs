namespace CalendarWork.App.ViewModels;

/// <summary>ViewModel이 자신을 보여 주는 창을 닫아 달라고 요청할 때. View는 WindowBehaviors.CloseOnRequest로 연결한다.</summary>
public interface ICloseRequest
{
    event Action? CloseRequested;
}
