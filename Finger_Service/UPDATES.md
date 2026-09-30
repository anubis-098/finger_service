# GitHub updates — v1.2.0

Repository: https://github.com/anubis-098/finger_service

## ผู้ใช้ Client

ติดตั้งรุ่นที่รองรับ updater ครั้งแรกโดยแตก ZIP ทั้งโฟลเดอร์ ปิดรุ่นเดิมแล้วเปิด `CJFingerService.exe` ใหม่ หลังจากนี้โปรแกรมตรวจ GitHub ตอนเปิด (หลัง 10 วินาที) และทุก 12 ชั่วโมง ใช้เมนู System tray → **ตรวจอัปเดต / Check updates** เพื่อตรวจเองและยืนยันติดตั้งได้ รุ่นใหม่จะดาวน์โหลด ตรวจ SHA-256 เทียบ GitHub Release asset และตรวจเลขรุ่นใน EXE ก่อนปิดตัวเอง ให้ helper สำรอง/แทนที่ไฟล์ แล้วเปิดใหม่

ไม่อัปเดตขณะมาโครทำงาน ไม่ลบ `%LOCALAPPDATA%\CJFingerService` ซึ่งเก็บ Settings, รหัสผ่าน, Token และคิว TXT ใช้ได้กับโฟลเดอร์ที่บัญชี Windows มีสิทธิ์เขียนเท่านั้น เมื่ออัปเดตเปิดใหม่ ตารางงานใช้ค่าที่บันทึกไว้ หากเวลาเริ่มผ่านไปแล้วจะเริ่มทำงานทันทีตามพฤติกรรมเดิม

สำรองโปรแกรมเดิมและ `install.log` อยู่ใน `%LOCALAPPDATA%\CJFingerService\updates\<id>\` หากแทนที่ไฟล์ล้มเหลว helper พยายามคืนไฟล์เดิม ไม่ใช่ระบบตรวจสุขภาพหรือ rollback หลังโปรแกรมรุ่นใหม่เปิดแล้วเกิดบั๊ก การตรวจ checksum ยืนยันไฟล์ตรงกับ asset ที่ GitHub รายงาน ไม่ใช่ Authenticode signature

## ผู้พัฒนา: เผยแพร่รุ่นใหม่

1. เก็บโค้ด `Finger_Service` ใน repository นี้ โดยไม่อัปโหลด settings.json, test-data, pending, logs หรือข้อมูลพนักงาน `.gitignore` เตรียมไว้แล้ว
2. เปลี่ยน `<Version>` ใน `FingerService.csproj` เช่น `1.2.1` ใช้เลขสามส่วน
3. รัน `powershell -ExecutionPolicy Bypass -File Finger_Service/release.ps1`
4. สร้าง GitHub Release แบบ Published (ไม่ใช่ Draft/Prerelease) ใช้ tag ให้ตรง เช่น `v1.2.1` แนบไฟล์ `release/CJFingerService-win-x64.zip` ชื่อตรงนี้เท่านั้น และแนบ `SHA256.txt` เพื่อให้ตรวจเองได้
5. ZIP ต้องมี `CJFingerService.exe`, `ocr.ps1`, `update.ps1`, `README.md` ที่ root เท่านั้น GitHub API ต้องคืน digest SHA-256 ของ ZIP มิฉะนั้น Client จะปฏิเสธ

หากต้องการ Build/Release อัตโนมัติ ให้คัดลอก `github-release.yml` ไป `.github/workflows/release.yml` ใน repository แล้ว push tag ใหม่ Workflow จะตรวจ version, build บน Windows และเผยแพร่ asset ด้วย GitHub Actions token ไม่ต้องแจก GitHub PAT ให้ Client

ตรวจรุ่นที่เผยแพร่แล้วที่ https://github.com/anubis-098/finger_service/releases รุ่น 1.2.0 เป็นรุ่นแรกที่รองรับ updater ต้องติดตั้งด้วยตนเองครั้งแรกก่อน หลังจากนั้นเมื่อมีรุ่นสูงกว่าจะตรวจพบและติดตั้งผ่านเมนู Check updates ได้

อ้างอิง API: https://docs.github.com/en/rest/releases/releases#get-the-latest-release
