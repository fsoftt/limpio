using System.Management;
using Limpio.Core.Models;

namespace Limpio.Core.Engine;

/// <summary>Turns restore point failures into readable, localized reasons. Never returns an empty text.</summary>
public static class RestorePointReasons
{
    public static LocalizedText Disabled(string drive) => new()
    {
        Es = $"La protección del sistema está desactivada en {drive}. Actívala para poder crear puntos de restauración.",
        En = $"System Protection is turned off on {drive}. Turn it on to be able to create restore points.",
    };

    /// <summary>Maps the return code of the WMI SystemRestore.CreateRestorePoint method.</summary>
    public static LocalizedText FromReturnCode(uint code) => code switch
    {
        5 => new() { Es = "Acceso denegado (código 5). Ejecuta Limpio como administrador.", En = "Access denied (code 5). Run Limpio as administrator." },
        112 => new() { Es = "No hay espacio suficiente en disco (código 112).", En = "Not enough disk space (code 112)." },
        183 => new() { Es = "Ya hay otra restauración del sistema en curso (código 183).", En = "Another System Restore operation is already running (code 183)." },
        1058 => new() { Es = "El servicio de Restaurar sistema está deshabilitado (código 1058).", En = "The System Restore service is disabled (code 1058)." },
        1460 => new() { Es = "Se agotó el tiempo de espera de Restaurar sistema (código 1460).", En = "System Restore timed out (code 1460)." },
        1717 => new() { Es = "No se encontró la interfaz de Restaurar sistema (código 1717).", En = "The System Restore interface was not found (code 1717)." },
        _ => new() { Es = $"Restaurar sistema devolvió el código {code}. Puede estar desactivado.", En = $"System Restore returned code {code}. It may be turned off." },
    };

    /// <summary>Describes an exception. Falls back to the exception type and HRESULT when the message is empty.</summary>
    public static LocalizedText FromException(Exception ex)
    {
        var hr = (uint)ex.HResult;
        var detail = (ex.Message ?? "").Trim();
        var hrText = $"HRESULT 0x{hr:X8}";
        string? hintEs = null, hintEn = null;

        if (ex is ManagementException me)
        {
            hrText += $", WMI {me.ErrorCode} (0x{(uint)me.ErrorCode:X8})";
            if (string.IsNullOrEmpty(detail)) detail = me.ErrorCode.ToString();
            switch (me.ErrorCode)
            {
                case ManagementStatus.AccessDenied:
                    hintEs = "Acceso denegado: ejecuta Limpio como administrador.";
                    hintEn = "Access denied: run Limpio as administrator.";
                    break;
                case ManagementStatus.InvalidNamespace:
                case ManagementStatus.InvalidClass:
                case ManagementStatus.NotFound:
                    hintEs = "Restaurar sistema no está disponible: probablemente la protección del sistema está desactivada.";
                    hintEn = "System Restore is not available: System Protection is probably turned off.";
                    break;
            }
        }
        else if (ex is UnauthorizedAccessException)
        {
            hintEs = "Acceso denegado: ejecuta Limpio como administrador.";
            hintEn = "Access denied: run Limpio as administrator.";
        }
        if (hintEs is null && hr == 0x80070422)
        {
            hintEs = "El servicio de Restaurar sistema o de instantáneas (VSS) está deshabilitado.";
            hintEn = "The System Restore or Volume Shadow Copy (VSS) service is disabled.";
        }

        var core = string.IsNullOrEmpty(detail) ? ex.GetType().Name : detail;
        var tail = $" ({ex.GetType().Name}, {hrText})";
        return new LocalizedText
        {
            Es = (hintEs is null ? "" : hintEs + " ") + core + tail,
            En = (hintEn is null ? "" : hintEn + " ") + core + tail,
        };
    }
}
