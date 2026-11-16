using UnscRelay.Core;
namespace UnscRelay.App;
public partial class MainWindow
{
    void InitializeEditorPreference()
    {
        var before=data.Settings;
        try {
            var automatic=before.EditorPreference=="auto" || before.EditorPreference.Length==0&&before.ExternalEditor.Length==0;
            data.Settings=EditorDiscovery.ApplyAutomatic(before,automatic?EditorDiscovery.FindNotepadPlusPlus():"");
            if(data.Settings!=before)store.Save(data);
        }catch(System.IO.IOException){data.Settings=before;}catch(UnauthorizedAccessException){data.Settings=before;}
    }
}
