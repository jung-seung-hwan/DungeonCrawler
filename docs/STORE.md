# Microsoft Store 출시 안내

DungeonTamheomga를 Microsoft Store에 올리는 순서입니다. 2026-11-10에 Microsoft Learn 공식 문서를 확인해 정리했습니다.
스토어 정책과 Partner Center 화면은 자주 바뀌므로, 화면이 이 안내와 다르면 화면과 아래 링크의 최신 문서를 따르세요.

- **계정 만들기, 로그인, 본인 확인, 약관 동의, 제출 버튼은 직접 하셔야 합니다.** 이 문서는 어디서 무엇을 누르고 무엇을 넣는지 알려 줍니다.
- 저장소가 해 주는 일: 빌드, 아이콘, MSIX 패키지, 스토어용 그림·스크린샷, 설명 초안.

---

## 1. 제출 방식 비교와 추천

| 방식 | 서명 | 호스팅·업데이트 | 이 프로젝트에 추가로 필요한 것 | 평가 |
|------|------|------|------|------|
| **MSIX 패키지** (Win32 빌드를 그대로 묶음) | 스토어가 무료로 다시 서명 | 스토어가 호스팅, 자동 업데이트 | 매니페스트 + `makeappx` (Windows SDK, 이 PC에 설치돼 있음) → `Tools\pack-store.ps1` | **추천** |
| EXE/MSI 설치 파일 | 직접 구매한 코드 서명 인증서 필요(자체 서명 불가) | 직접 HTTPS로 호스팅, 버전마다 새 URL, 업데이트도 직접 | 무인 설치 지원 설치 프로그램 제작, 인증서 구입, 파일 서버 | 비용·수고가 가장 큼 |
| GDK 게임 (MSIXVC) | 스토어 | 스토어 | GDK 설치, `MicrosoftGame.config`, `makepkg`, EKB·심볼·검증 파일 업로드 | Microsoft가 Win32 게임에 권장하는 경로지만 준비물이 많음 |
| UWP 빌드 | 스토어 | 스토어 | 유니티 UWP 모듈 + Visual Studio, 빌드 대상 변경에 따른 재검증 | 얻는 것 없이 빌드 방식만 바뀜 |

**추천: MSIX 패키지.** 지금의 `Builds\Windows` 빌드를 바꾸지 않고 그대로 묶을 수 있고, 인증서 구입·파일 서버가 필요 없으며, 필요한 도구가 이미 설치돼 있습니다.

근거(공식 문서):
- MSIX 제출은 스토어가 다시 서명하므로 인증서를 살 필요가 없고 스토어가 호스팅·업데이트를 맡음. EXE/MSI는 Microsoft Trusted Root Program에 속한 CA 인증서로 직접 서명하고 직접 호스팅해야 하며 스토어 업데이트가 없음 —
  [Choose a distribution path](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/choose-distribution-path),
  [App package requirements for MSIX app](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-package-requirements),
  [App package requirements for MSI/EXE app](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msi/app-package-requirements)
- 데스크톱(Win32) 앱을 명령줄 도구로 MSIX로 묶는 방법 — [Generating MSIX package components](https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-manual-conversion)
- GDK 경로(ID@Xbox 불필요, Xbox 서비스 선택 사항, MSIXVC 형식) — [Publish PC games to the Microsoft Store using GDK](https://learn.microsoft.com/en-us/windows/apps/publish/whats-new-game-publishing)

**알아 둘 위험 (확인하지 못한 것)**
- 위 GDK 문서는 "기존 Win32 게임은 **GDK game**, UWP·PWA 게임은 **MSIX or PWA game**"을 고르라고 안내합니다. Win32 게임을 MSIX로 묶어 내는 것을 금지한다는 문구는 찾지 못했고 MSIX 문서는 Win32 앱 패키징을 정식으로 다루지만,
  **Partner Center가 지금 Win32 게임의 MSIX 제출을 그대로 받아 주는지는 계정이 없어 확인하지 못했습니다.** 3단계(이름 예약)에서 선택지가 다르게 보이거나 인증에서 반려되면 알려 주세요. 그때는 GDK 경로로 바꿉니다(빌드는 그대로, 묶는 도구만 달라짐).
- 인증 통과 여부는 제출해 봐야 압니다.

---

## 2. 개발자 계정 만들기 (직접, 무료)

문서: [Steps to open a Microsoft Store developer account](https://learn.microsoft.com/en-us/windows/apps/publish/partner-center/open-a-developer-account)

1. **반드시 <https://storedeveloper.microsoft.com> 에서 시작**합니다. 문서에 따르면 다른 입구(Partner Center 직접 접속 등)는 예전 유료 절차를 보여 줍니다.
2. **Get started for free** → **Individual developer**(개인) 선택.
   - 개인 계정: 취미·개인 프로젝트용. 회사 계정: 사업자 명의. **개인 → 회사 전환은 지원되지 않으므로** 사업자로 낼 계획이면 처음부터 회사 계정으로 만드세요.
   - 새 절차에서는 등록비가 없습니다(개인·회사 모두).
3. 개인 Microsoft 계정으로 로그인(없으면 생성).
4. 본인 확인: 정부 발급 신분증과 셀카를 휴대폰으로 촬영.
5. 프로필 작성. 여기서 정하는 **게시자 표시 이름(Publisher display name)** 이 스토어에 "만든 사람"으로 표시됩니다.
6. **Go to Partner Center dashboard** → 같은 계정 선택 → *Apps & Games* 화면. 안 보이면 5분쯤 뒤 새로 고침하거나 <https://aka.ms/submitwindowsapp> 로 접속.

---

## 3. 게임 이름 예약하고 제품 ID 받기 (직접)

1. [Partner Center](https://partner.microsoft.com/dashboard) → **Apps and games** → **+ New product**.
2. 제품 종류에서 **MSIX or PWA** 쪽(게임이면 *MSIX or PWA game*, 없으면 *MSIX or PWA app*)을 고릅니다. *GDK game*이나 *EXE or MSI app*이 아닙니다.
3. 이름에 `DungeonTamheomga` 입력 → **Check availability** → **Reserve product name**.
   - 이미 쓰는 이름이면 다른 이름을 정해야 하고, 그 이름을 `Tools\store\store.config.json`의 `DisplayName`에도 똑같이 넣어야 합니다.
4. 왼쪽 **Product management → Product identity** 에서 아래 세 값을 복사해 [Tools/store/store.config.json](../Tools/store/store.config.json)에 넣습니다. 대소문자·공백·구두점까지 같아야 합니다.

   | Partner Center 표시 | store.config.json |
   |------|------|
   | Package/Identity/Name | `IdentityName` |
   | Package/Identity/Publisher (`CN=...`) | `Publisher` |
   | Package/Properties/PublisherDisplayName | `PublisherDisplayName` |

   이 값들은 비밀이 아닙니다(배포되는 모든 패키지 안에 들어 있음). 저장소에 커밋해도 됩니다.

---

## 4. 패키지와 그림 만들기 (저장소 스크립트)

유니티 에디터를 닫고 실행합니다.

```powershell
.\Tools\build.ps1            # 윈도우 빌드 (아이콘이 없으면 Assets\Icon\AppIcon.png 도 만듦)
.\Tools\pack-store.ps1       # Builds\Store\DungeonTamheomga_<버전>_x64.msix
.\Tools\store-assets.ps1     # Builds\Store\listing\ 에 로고·포스터·스크린샷(한/영, 1920x1080)
```

- `pack-store.ps1`은 `store.config.json`에 `REPLACE_ME`가 남아 있으면 멈춥니다. 값을 넣기 전에 절차만 확인하려면 `-AllowPlaceholder`(이 결과물은 제출 불가).
- 버전은 `ProjectSettings.asset`의 `bundleVersion`(지금 `1.0.0`)에서 가져와 `1.0.0.0`이 됩니다. **업데이트를 낼 때마다 `bundleVersion`을 올려야** 합니다(넷째 자리는 스토어 몫이라 항상 0).
- 패키지는 서명하지 않습니다. 스토어가 인증 통과 뒤 서명합니다. 그래서 이 `.msix`를 더블클릭해 설치할 수는 없습니다.
- 아이콘을 바꾸려면 유니티 메뉴 **Tools → Dungeon → Generate App Icon**(`ProjectTools.GenerateAppIcon`)을 고치고 다시 실행합니다.

**(선택) 올리기 전에 패키지 상태로 실행해 보기** — Windows 설정에서 *개발자 모드*를 켠 뒤(시스템 설정이므로 직접):

```powershell
Add-AppxPackage -Register .\Builds\Store\layout\AppxManifest.xml
```

시작 메뉴에서 DungeonTamheomga를 실행해 보고, 끝나면 `Get-AppxPackage *DungeonTamheomga* | Remove-AppxPackage`로 지웁니다.
Windows App Certification Kit(이 PC에 설치돼 있음, 시작 메뉴에서 검색)으로 미리 검사할 수도 있습니다.

---

## 5. 제출서 작성 (직접)

제품 개요 화면에서 **Start submission**. 문서: [Create an app submission for your MSIX app](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/create-app-submission)

### Pricing and availability
- Markets: 기본값(모든 시장) 또는 원하는 나라.
- Base price: **무료/유료는 직접 정하세요.** 유료라면 정산·세금 프로필을 Partner Center 계정 설정에서 따로 채워야 합니다(이 문서에서는 다루지 않음).

### Properties
- Category: **Games**, 하위 분류는 *Role playing* 또는 *Strategy* 중 맞는 쪽.
- "앱이 개인 정보를 접근·수집·전송합니까?" → **No**. 이 게임은 네트워크 기능·계정·개인정보 수집이 없고 기록은 PC 안에만 저장합니다.
  - 개인정보를 수집하지 않으면 개인정보 처리방침 URL은 필수가 아닙니다. 다만 문서에 "No라고 답해도 요구될 수 있다"고 되어 있으므로, 요구되면 [store/privacy-policy.md](store/privacy-policy.md)의 글을 붙여 넣거나 웹에 올려 그 주소를 넣으세요.
    ([Privacy policy and support info](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/support-info))
- System requirements(선택): 키보드 필수, 마우스 권장. 최소 사양은 직접 시험한 범위만 적으세요(이 PC 외의 사양에서는 확인하지 않았습니다).

### Age ratings
- IARC 설문에 사실대로 답합니다(필수). 문서: [Age ratings](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/age-ratings)
- 답할 때 참고할 게임 내용: 판타지 몬스터와 싸우는 픽셀 그림 전투(피·잔혹 묘사 없음), 욕설·성적 내용·도박·약물 없음, 온라인 기능·이용자 간 소통·위치 공유·게임 내 구매 없음.
- 설문 결과로 나라별 등급이 한꺼번에 나옵니다. 한국 등급이 어떻게 표시되는지는 결과 화면에서 확인하세요(미리 확인하지 못했습니다). 특정 시장에 제공할 수 없다는 안내가 뜨면 그 내용을 알려 주세요.

### Packages
- `Builds\Store\DungeonTamheomga_<버전>_x64.msix`를 끌어다 놓습니다. 검증이 실패하면 오류 문구를 그대로 알려 주세요(대부분 3단계의 제품 ID 값 불일치).

### Store listings
언어별로 하나씩 만듭니다(영어, 한국어). 글은 [store/listing-en.md](store/listing-en.md), [store/listing-ko.md](store/listing-ko.md)에서 복사합니다.

| 항목 | 넣을 것 |
|------|------|
| Description (필수) | 설명 초안 |
| Screenshots (필수, 1장 이상·4장 이상 권장) | `Builds\Store\listing\screenshots\<언어>\` 에서 4~8장. 올리기 전에 한 장씩 열어 확인 |
| Store logos — 2:3 Poster art | `poster_2x3_720x1080.png` (또는 `1440x2160`) |
| Store logos — 1:1 Box art | `boxart_1x1_1080x1080.png` |
| Store logos — 1:1 App tile icon | `logo_1x1_300x300.png` |
| Short description, Features, Keywords | 초안 참고 |

규격 문서: [App screenshots, images, and trailers](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/screenshots-and-images) — 스크린샷은 PNG, 데스크톱 1366×768 이상, 50MB 이하.

### Submission options
- **Restricted capabilities**: 패키지가 `runFullTrust`를 선언하므로 사유를 적어야 합니다. 예:
  > This is a Win32 desktop game built with Unity (Windows Standalone x64) and packaged as MSIX. runFullTrust is required to run the Win32 executable. The game does not access the network or any user data outside its own settings.
- Notes for certification(선택): "No account or network connection required. Keyboard and mouse. Press any key on the title screen, then pick a class with keys 1-3." 정도.

### 제출
모든 항목이 완료로 표시되면 **Submit for certification**. 결과는 메일과 Partner Center에 표시됩니다. 반려되면 사유를 알려 주세요.

---

## 6. 저장 데이터에 대해

- 게임 기록은 `PlayerPrefs`(레지스트리 `HKCU\Software\<회사 이름>\DungeonTamheomga`)에 저장됩니다.
- MSIX로 설치된 게임이 `HKCU`에 쓰는 값은 이용자·앱별 전용 공간에 따로 저장됩니다(쓸 때 복사). **업데이트해도 유지되고, 게임을 제거하면 함께 지워집니다.**
  스토어 버전이 쓴 기록은 `Builds\Windows`로 실행하는 개발용 빌드에서는 보이지 않습니다.
  ([Understanding how packaged desktop apps run on Windows](https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-behind-the-scenes)의 Registry 절 — 실제 설치로 확인하지는 못했습니다.)
- 유니티 `companyName`이 아직 `DefaultCompany`입니다. **출시 뒤에 바꾸면 이용자의 기록 위치가 바뀌어 기록이 사라진 것처럼 보이므로, 바꾸려면 첫 제출 전에** 게시자 이름으로 바꾸는 것이 좋습니다(바꾸면 이 PC의 개발용 기록도 한 번 옮겨야 함).

## 7. 업데이트 내는 법

1. `ProjectSettings.asset`의 `bundleVersion`을 올림(예: `1.0.1`).
2. `.\Tools\build.ps1` → `.\Tools\pack-store.ps1`.
3. Partner Center에서 제품 → **Update** → Packages에 새 `.msix` 추가 → 제출.
