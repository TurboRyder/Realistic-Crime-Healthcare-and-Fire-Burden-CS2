// Soft bridge to CustomChirps API (mod 121812) with reflection. No hard reference required.
// Adapted from the official pattern (ruzbeh0/CustomChirps, as used by Time2Work/InfoLoom).
// If Custom Chirps isn't installed, IsAvailable is false and calls are safely skipped.

using System;
using System.Reflection;
using Unity.Entities;

namespace RealisticCrimeSystem.Bridge
{
    /// <summary>
    /// Local mirror of CustomChirps.Systems.DepartmentAccount (names must match).
    /// </summary>
    public enum DepartmentAccountBridge
    {
        Electricity,
        FireRescue,
        Roads,
        Water,
        Communications,
        Police,
        PropertyAssessmentOffice,
        Post,
        BusinessNews,
        CensusBureau,
        ParkAndRec,
        EnvironmentalProtectionAgency,
        Healthcare,
        LivingStandardsAssociation,
        Garbage,
        TourismBoard,
        Transportation,
        Education
    }

    /// <summary>
    /// Reflection-based bridge to CustomChirps API. No hard reference required.
    /// </summary>
    public static class CustomChirpsBridge
    {
        private static bool _resolved;
        private static Type _apiType;
        private static Type _deptEnumType;
        private static MethodInfo _postChirp;

        public static bool IsAvailable
        {
            get { EnsureResolve(); return _apiType != null && _deptEnumType != null && _postChirp != null; }
        }

        public static bool PostChirp(string text, DepartmentAccountBridge department, Entity entity, string customSenderName = null)
        {
            EnsureResolve();
            if (_postChirp == null) return false;
            try
            {
                object realDept;
                try { realDept = Enum.Parse(_deptEnumType, department.ToString(), false); }
                catch { realDept = Enum.Parse(_deptEnumType, "Police", true); }
                _postChirp.Invoke(null, new object[] { text ?? string.Empty, realDept, entity, customSenderName });
                return true;
            }
            catch { return false; }
        }

        private static void EnsureResolve()
        {
            if (_resolved) return;
            _resolved = true;
            _apiType = Type.GetType("CustomChirps.Systems.CustomChirpApiSystem, CustomChirps") ?? FindType("CustomChirps.Systems.CustomChirpApiSystem");
            _deptEnumType = Type.GetType("CustomChirps.Systems.DepartmentAccount, CustomChirps") ?? FindType("CustomChirps.Systems.DepartmentAccount");
            if (_apiType != null)
                _postChirp = _apiType.GetMethod("PostChirp", BindingFlags.Public | BindingFlags.Static);
        }

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var t = asm.GetType(fullName, false);
                    if (t != null) return t;
                }
                catch { }
            }
            return null;
        }
    }
}
