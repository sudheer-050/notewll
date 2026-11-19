import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
APP = ROOT / "build" / "Notewll.exe"


def parse(tmp_path, body):
    ics = tmp_path / "invite.ics"
    ics.write_text("BEGIN:VCALENDAR\r\nVERSION:2.0\r\nBEGIN:VEVENT\r\n" + body + "END:VEVENT\r\nEND:VCALENDAR\r\n", encoding="utf-8")
    out = subprocess.run([str(APP), "--calendar-parse", str(ics)], capture_output=True, text=True, timeout=60)
    assert out.returncode == 0, out.stderr
    return json.loads(out.stdout)[0]


def test_invite_keeps_organizer_attendees_and_meeting_link(tmp_path):
    item = parse(tmp_path,
        "UID:a1\r\nDTSTART:20261120T150000Z\r\nSUMMARY:Design review\r\n"
        "ORGANIZER;CN=Priya Shah:mailto:priya@example.com\r\n"
        "ATTENDEE;CN=\"Alex Kim\";PARTSTAT=ACCEPTED:mailto:alex@example.com\r\n"
        "ATTENDEE;RSVP=TRUE:mailto:sam@example.com\r\n"
        "DESCRIPTION:Join: https://meet.google.com/abc-defg-hij\r\n")
    assert item["Title"] == "Design review"
    assert item["Organizer"] == "Priya Shah <priya@example.com>"
    assert item["Attendees"] == ["Alex Kim <alex@example.com>", "sam@example.com"]
    assert item["Url"] == "https://meet.google.com/abc-defg-hij"


def test_invite_without_people_still_imports(tmp_path):
    item = parse(tmp_path, "UID:a2\r\nDTSTART:20261120T150000Z\r\nSUMMARY:Solo focus\r\n")
    assert item["Organizer"] is None and item["Attendees"] == [] and item["Url"] == ""


def test_meeting_detector_reports_without_crashing():
    out = subprocess.run([str(APP), "--meeting-detect"], capture_output=True, text=True, timeout=60)
    assert out.returncode == 0 and "meeting:" in out.stdout
