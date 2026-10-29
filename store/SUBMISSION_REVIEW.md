# Notewll submission readiness review

## Current Partner Center state

- Product: Notewll / myhai, Store ID 9MTSFVDZD1C2.
- Submission 1152921505702228860 remains In draft. Submit for certification is available.
- All six sections are Complete: Pricing and availability, Properties, Age ratings, Packages, Store listings, Submission options.
- Package 0.10.2.0 x64 is Validated; local identity and SHA256 match the upload build metadata. Store package verification passed again.
- Pricing: USD 0, all worldwide markets, public and discoverable after publication.
- Age ratings: saved IARC rating, ESRB Everyone, PEGI 3+, and regional equivalents. Earlier notes saying ratings were unfinished are historical.
- Properties: Productivity, microphone required, x64, personal-information use declared, Notewll-specific privacy text supplied, support email saved.
- English listing: refreshed release notes, corrected original/modified text feature, four saved 1914 x 1359 screenshots with captions.
- runFullTrust explanation and detailed reviewer notes are saved.
- Publishing hold: do not publish until Publish now is selected. Certification does not automatically make this draft public.

## Fixed in this update

- Cleared the Games-only recording/broadcast-clips declaration and verified it remained unchecked after reloading Properties.
- Updated and reload-verified reviewer instructions to Preferences > Recording, Home > Try dictation, and Original text / Modified text views.
- Published the existing Notewll policy at https://myhai.org/projects/notewll/privacy.html and corrected the Notewll product footer. The shared catalog generator owns the page; unrelated product policies are unchanged. CMS healthy, revision 3cc2036215c06955; public page and footer verified in browser.
- About, tray and release-notes version headings now use assembly metadata. Store compilation generates that metadata from the MSIX version and rejects a mismatch.
- Uploaded and saved 0.10.2.0, replacing 0.10.1.0. Partner Center reports Validated and all six sections Complete. Manual publishing hold remains selected.
- Store artifact test passed; exact staged executable passed themes, recording shortcuts, UX and offline speech checks. Executable file version equals package version 0.10.2.0.

## Installed version

Get-AppxPackage confirms a Store-format Notewll 0.10.0.0 installation with Status Ok on this PC. The uploaded 0.10.2.0 package is not installed here. Back up notes and verify the upgrade to 0.10.2.0 preserves them during desktop acceptance.

## Remaining release acceptance

- Test the actual installed Store-format package on a normal Windows desktop or clean VM, not only the staged executable or direct-download installer.
- Verify microphone dictation, silence, global shortcuts, exact typing into Notepad and browser fields, and meeting capture with actual playback.
- Verify notes, export, startup on/off, update preserving notes, uninstall, and offline launch.
- Run Windows App Certification Kit and retain its report. No WACK pass is currently recorded.
- The latest full regression run is 173 passed, 2 failed, 3 skipped. Both live microphone tests return MMSYSERR 1 in this session, also reproduced with the previously installed executable. Desktop typing injection and the unavailable legacy Holly service account for the skips. These are not a clean physical acceptance result.
- UI audit: 188 native window captures, zero detected layout findings. Store-specific theme, shortcut and UX checks and bundled offline speech loading passed.

## Next sequence

1. Metadata, reviewer instructions, privacy link, version display and updated upload are complete.
2. Complete signed-package desktop acceptance and WACK; fix any failures and upload a higher package version if code changes.
3. Submit for certification. Microsoft will review the package and restricted capability request.
4. After approval, select Publish now when ready, verify the public Store listing/install, and add the Store link to the website.

This update saved the Partner Center draft and published the product-specific privacy page. It did not submit for certification, publish the Store app, change legal agreements, install a test package or change certificate trust.
