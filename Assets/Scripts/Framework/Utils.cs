using UnityEngine;

public static class Utils
{
    private static bool storeJumped;
    public static bool HasStoreJumped => storeJumped;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        storeJumped = false;
    }

    public static void OpenStore()
    {
        storeJumped = true;
        Luna.Unity.LifeCycle.GameEnded();
        Luna.Unity.Playable.InstallFullGame();
    }
}
