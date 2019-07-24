using System;
using System.IO;

namespace Voc999Creator
{
	static class Program
	{
		static void Main(string[] args)
		{
			var names = new[] {
				"Load",          "UnLoad",      "ScriptID",    "DisposeScript", "Clone",            "DisposeClone",  "IsObject",      "RespondsTo",
				"DrawPic",       "Show",        "PicNotValid", "Animate",       "SetNowSeen",       "NumLoops",      "NumCels",       "CelWide",
				"CelHigh",       "DrawCel",     "AddToPic",    "NewWindow",     "GetPort",          "SetPort",       "DisposeWindow", "DrawControl",
				"HiliteControl", "EditControl", "TextSize",    "Display",       "GetEvent",         "GlobalToLocal", "LocalToGlobal", "MapKeyToDir",
				"DrawMenuBar",   "MenuSelect",  "AddMenu",     "DrawStatus",    "Dummy",            "Dummy",         "Dummy",         "HaveMouse",
				"SetCursor",     "SaveGame",    "RestoreGame", "RestartGame",   "GameIsRestarting", "DoSound",       "NewList",       "DisposeList",
				"NewNode",       "FirstNode",   "LastNode",    "EmptyList",     "NextNode",         "PrevNode",      "NodeValue",     "AddAfter",
				"AddToFront",    "AddToEnd",    "FindKey",     "DeleteKey",     "Random",           "Abs",           "Sqrt",          "GetAngle",
				"GetDistance",   "Wait",        "GetTime",     "StrEnd",        "StrCat",           "StrCmp",        "StrLen",        "StrCpy",
				"Format",        "GetFarText",  "ReadNumber",  "BaseSetter",    "DirLoop",          "CantBeHere",    "OnControl",     "InitBresen",
				"DoBresen",      "Platform",    "SetJump",     "SetDebug",      "InspectObj",       "ShowSends",     "Dummy",         "ShowFree",
				"MemoryInfo",    "StackUsage",  "Profiler",    "GetMenu",       "SetMenu",          "GetSaveFiles",  "GetCWD",        "CheckFreeSpace",
				"ValidPath",     "CoordPri",    "StrAt",       "DeviceInfo",    "GetSaveDir",       "CheckSaveGame", "ShakeScreen",   "FlushResources",
				"SinMult",       "CosMult",     "SinDiv",      "CosDiv",        "Graph",            "Joystick",      "ShiftScreen",   "Palette",
				"MemorySegment", "PalVary",     "Memory",      "ListOps",       "FileIO",           "DoAudio",       "DoSync",        "AvoidPath",
				"Sort",          "ATan",        "Lock",        "StrSplit",      "Message",          "IsItSkip",      "MergePoly",     "ResCheck",
				"AssertPalette", "TextColors",  "TextFonts",   "Record",        "PlayBack",         "ShowMovie",     "SetVideoMode",  "SetQuitStr",
				"DbugStr"
			};
			if (args.Length != 0 && File.Exists(args[0]))
			{
				names = File.ReadAllLines(args[0]);
			}
			var offsets = new Int16[names.Length];
			using (var nines = new BinaryWriter(File.Open("999.voc", FileMode.OpenOrCreate)))
			{
				nines.Write((Int16)0x86); //ResType
				//All offsets start here.
				nines.Write((Int16)names.Length);
				var offsetToNames = 4 /* for count */ + (names.Length * 2);
				nines.BaseStream.Seek(offsetToNames, SeekOrigin.Begin);
				for (var i = 0; i < names.Length; i++)
				{
					var name = names[i];
					offsets[i] = (Int16)(nines.BaseStream.Position - 2);
					nines.Write((Int16)name.Length);
					nines.Write(name.ToCharArray());
				}
				nines.Write((byte)0x1A);
				nines.BaseStream.Seek(4, SeekOrigin.Begin);
				for (var i = 0; i < offsets.Length; i++)
				{
					nines.Write(offsets[i]);
				}
			}
		}
	}
}
