using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>Metadata for one manual slot. The autosave is stored separately.</summary>
public sealed class CampaignSaveSlotInfo
{
    public int slot;
    public bool isEmpty;
    public bool isCorrupt;
    public string error;
    public string savedAtUtc;
    public double playTimeSeconds;
    public string mapScene;
    public string locationName;

    public DateTime? SavedAtLocal
    {
        get
        {
            if (DateTimeOffset.TryParse(savedAtUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var time))
                return time.LocalDateTime;
            return null;
        }
    }
}

/// <summary>
/// Reads and atomically writes independent manual snapshots. Callers supply the
/// directory so validation can use a temporary folder without touching live saves.
/// </summary>
public static class CampaignSaveSlots
{
    public const int MaximumSlots = 99;
    const int EnvelopeVersion = 1;
    static readonly double MaximumPlayTimeSeconds = TimeSpan.MaxValue.TotalSeconds - 1;

    [Serializable]
    sealed class SlotFile
    {
        public int version;
        public int slot;
        public string savedAtUtc;
        public double playTimeSeconds;
        public string mapScene;
        public string locationName;
        public CampaignSave data;
    }

    public static List<CampaignSaveSlotInfo> ListSlots(string directory, int count)
    {
        count = ClampCount(count);
        var slots = new List<CampaignSaveSlotInfo>(count);
        for (int slot = 1; slot <= count; slot++)
        {
            ReadSlot(directory, slot, count, out _, out var info, out _);
            slots.Add(info);
        }
        return slots;
    }

    public static bool TrySave(string directory, int slot, int slotCount, CampaignSave data,
        double playTimeSeconds, string locationName, out string error)
    {
        error = null;
        string temporaryPath = null;
        bool ownsTemporaryFile = false;
        try
        {
            string path = SlotPath(directory, slot, slotCount);
            if (!ValidData(data, out error)) return false;
            if (!ValidPlayTime(playTimeSeconds))
            { error = "Thời gian chơi không hợp lệ."; return false; }
            if (string.IsNullOrWhiteSpace(data.currentMapScene))
            { error = "Bản lưu chưa có bản đồ khám phá."; return false; }

            // Serialize/deserialize before adding metadata: the live campaign is
            // never changed by a slot write, including an unsuccessful overwrite.
            var snapshot = JsonUtility.FromJson<CampaignSave>(JsonUtility.ToJson(data));
            if (!ValidData(snapshot, out error)) return false;
            string savedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            string location = string.IsNullOrWhiteSpace(locationName) ? snapshot.currentMapScene : locationName;
            snapshot.playTimeSeconds = playTimeSeconds;
            snapshot.savedAtUtc = savedAtUtc;
            snapshot.saveLocationName = location;
            var file = new SlotFile
            {
                version = EnvelopeVersion,
                slot = slot,
                savedAtUtc = savedAtUtc,
                playTimeSeconds = playTimeSeconds,
                mapScene = snapshot.currentMapScene,
                locationName = location,
                data = snapshot
            };
            string json = JsonUtility.ToJson(file, true);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                ownsTemporaryFile = true;
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, true))
                {
                    writer.Write(json);
                    writer.Flush();
                }
                stream.Flush(true);
            }
            // File.Replace keeps the preceding snapshot as .bak. Never delete
            // the destination to implement an overwrite: a failed write must
            // leave the previous primary file available.
            if (File.Exists(path)) File.Replace(temporaryPath, path, path + ".bak");
            else File.Move(temporaryPath, path);
            ownsTemporaryFile = false;
            return true;
        }
        catch (Exception exception)
        {
            error = "Không thể lưu vào ô này: " + exception.Message;
            return false;
        }
        finally
        {
            if (ownsTemporaryFile && temporaryPath != null)
            {
                try { File.Delete(temporaryPath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    public static bool TryLoad(string directory, int slot, int slotCount,
        out CampaignSave data, out CampaignSaveSlotInfo info, out string error)
    {
        bool loaded = ReadSlot(directory, slot, slotCount, out var file, out info, out error);
        data = loaded ? file.data : null;
        if (loaded)
        {
            // The envelope's metadata also travels with the resumed campaign.
            data.playTimeSeconds = file.playTimeSeconds;
            data.savedAtUtc = file.savedAtUtc;
            data.saveLocationName = file.locationName;
        }
        else if (info.isEmpty) error = "Ô lưu này chưa có dữ liệu.";
        return loaded;
    }

    static bool ReadSlot(string directory, int slot, int slotCount, out SlotFile file,
        out CampaignSaveSlotInfo info, out string error)
    {
        file = null;
        error = null;
        info = new CampaignSaveSlotInfo { slot = slot, isEmpty = true };
        try
        {
            string path = SlotPath(directory, slot, slotCount);
            string json;
            try { json = File.ReadAllText(path, Encoding.UTF8); }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
            info.isEmpty = false;
            file = JsonUtility.FromJson<SlotFile>(json);
            if (file == null || file.version != EnvelopeVersion || file.slot != slot)
                throw new InvalidDataException("Định dạng hoặc số ô lưu không hợp lệ.");
            if (string.IsNullOrWhiteSpace(file.savedAtUtc) || !DateTimeOffset.TryParse(file.savedAtUtc,
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out _))
                throw new InvalidDataException("Ngày giờ lưu không hợp lệ.");
            if (!ValidPlayTime(file.playTimeSeconds))
                throw new InvalidDataException("Thời gian chơi không hợp lệ.");
            if (!ValidData(file.data, out var dataError)) throw new InvalidDataException(dataError);
            if (string.IsNullOrWhiteSpace(file.mapScene) || !string.Equals(file.mapScene, file.data.currentMapScene, StringComparison.Ordinal))
                throw new InvalidDataException("Bản đồ trong ô lưu không hợp lệ.");
            info.savedAtUtc = file.savedAtUtc;
            info.playTimeSeconds = file.playTimeSeconds;
            info.mapScene = file.mapScene;
            info.locationName = string.IsNullOrWhiteSpace(file.locationName) ? file.mapScene : file.locationName;
            return true;
        }
        catch (Exception exception)
        {
            file = null;
            info.isEmpty = false;
            info.isCorrupt = true;
            info.error = error = "Không thể đọc ô lưu: " + exception.Message;
            // Deliberately do not silently load .bak: the user selected the
            // primary snapshot shown in the slot list.
            return false;
        }
    }

    static bool ValidData(CampaignSave data, out string error)
    {
        error = "Dữ liệu tiến trình không hợp lệ.";
        if (data == null || data.version < 1 || data.version > 5 || data.party == null || data.party.Count == 0 ||
            data.inventory == null || data.collected == null) return false;
        foreach (var member in data.party)
            if (member == null || member.prefabIndex < 0) return false;
        error = null;
        return true;
    }

    static int ClampCount(int count) => Math.Max(1, Math.Min(MaximumSlots, count));

    static bool ValidPlayTime(double seconds) => !double.IsNaN(seconds) && !double.IsInfinity(seconds) &&
        seconds >= 0 && seconds <= MaximumPlayTimeSeconds;

    static string SlotPath(string directory, int slot, int count)
    {
        if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Thư mục lưu game không hợp lệ.", nameof(directory));
        if (slot < 1 || slot > ClampCount(count)) throw new ArgumentOutOfRangeException(nameof(slot), "Ô lưu nằm ngoài danh sách.");
        return Path.Combine(Path.GetFullPath(directory), "adventure-slot-" + slot.ToString("D2", CultureInfo.InvariantCulture) + ".json");
    }
}
