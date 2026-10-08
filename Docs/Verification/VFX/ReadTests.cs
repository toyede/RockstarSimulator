return new { runtimeDone=UnityEditor.SessionState.GetBool("VfxRuntimeDone",false), runtime=UnityEditor.SessionState.GetString("VfxRuntimeChecks",""),
    asyncDone=UnityEditor.SessionState.GetBool("VfxAsyncDone",false), asyncChecks=UnityEditor.SessionState.GetString("VfxAsyncChecks","") };
