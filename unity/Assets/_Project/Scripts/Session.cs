using System;
using UnityEngine;

public static class Session
{
    private const string AccountIdKey = "account_id";

    private static string _accountId;

    public static int ServerId { get; set; }

    public static string AccountId
    {
        get
        {
            if (_accountId == null)
            {
                _accountId = PlayerPrefs.GetString(AccountIdKey, "");
                if (_accountId == "")
                {

                    _accountId = Guid.NewGuid().ToString();
                    PlayerPrefs.SetString(AccountIdKey, _accountId);
                    PlayerPrefs.Save();
                }
            }
            return _accountId;
        }
    }

    public static int CharacterId;

    // Reload Domain is off, so statics survive between Phere.
    // [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    // private static void Reset()
    // {
    //     ServerId = 0;
    //     _accountId = null;
    // }
}
