# NUNBORA: Frostblind

<img src="Assets\_Project\Assets\04_UI\main logo\NUNBORA dark.ver.png">

## 목차

1. [💡 프로젝트 소개](#-프로젝트-소개)

2. [📝 주요 기능](#-주요-기능)

3. [🎬 시연 영상](#-시연-영상)

4. [👋 팀원 소개](#-팀원-소개)

5. [🌐 시스템 구조](#-시스템-구조)

6. [🛠 기술 스택](#-기술-스택)

7. [🚀 실행 가이드](#-실행-가이드)

8. [📂 폴더 구조](#-폴더-구조)

9. [📑 출품 및 퍼블리싱 계획](#-출품-및-퍼블리싱-계획)

## 💡 프로젝트 소개

> **목적이 이끄는 하드코어 소음 통제 생존 게임**

- **NUNBORA: Frostblind**는 그린란드 내륙의 가상 연구 시설(SDI)을 배경으로 하는 **4인 협동 생존 PC 게임**입니다. 지하 3km 빙하 코어 시추 작업 중 봉인된 고대 생태계가 유출된 재난 상황 속에서 플레이어들은 극한의 추위와 괴생명체의 위협으로부터 살아남아야 합니다.

- **"Sound is Death"**. 걷기, 문 열기 등의 의도적인 행동뿐만 아니라 배고픔과 추위로 인한 기침, 꼬르륵 소리 등 통제 불능의 신체 소음까지 데시벨(dB) 수치로 환산되어 시각이 퇴화하고 청각이 극도로 발달한 괴생명체를 유인합니다.

- 단순한 '목적 없는 무한 생존'을 탈피하여, 매일 밤 발생하는 치명적인 '눈보라'를 뚫고 엔딩 재료를 수집해 탈출 또는 괴물 봉인이라는 명확한 최종 목표(엔딩) 를 향해 나아가는 차별화된 경험을 제공합니다.

## 📝 주요 기능

### 🤫 극단적 소음 통제 시스템 (Sound is Death)

모든 행동이 데시벨(dB) 수치로 계산되어 물리적인 전파와 장애물 차단 모델을 거쳐 괴물을 유인합니다. 플레이어는 "어디로 갈까"가 아닌 "어떤 소리를 낼까"를 끊임없이 계산하며 극도의 청각적 압박감을 경험하게 됩니다.

<!--
### 🌪️ 절차적 랜덤 맵 생성 (Procedural Map Generation)

매일 밤 발생하는 '눈보라' 이벤트 이후 맵의 지형, 구조물, 아이템 위치가 랜덤하게 재생성됩니다. 펄린 노이즈(Perlin Noise)와 2-Pass 지형 평탄화 알고리즘을 통해 매 탐험마다 새로운 긴장감과 탐험의 재미를 선사합니다.
-->

### 🧠 계층적 상태 기계(FSM) 기반 적 AI & 플레이어 제어

단순한 시야 회피가 아닌 의심도(Suspicion) 누적에 따른 2페이즈 수색(Search) 등 자연스러운 AI 행동 패턴을 구현했습니다. 플레이어 역시 1P/3P 애니메이터 동시 제어 및 FSM을 통해 정교한 조작감과 시각적 동기화를 제공합니다.

### 🎒 테트리스 스타일 그리드 인벤토리

아이템의 고유 사이즈와 회전(R키) 배치를 지원하는 공간 계산 기반 인벤토리 시스템입니다. 드래그 앤 드롭, 아이템 스택 병합, 하단 퀵슬롯과의 다이렉트 교환 기능을 UI Toolkit의 절대 좌표 포지셔닝으로 부드럽게 구현했습니다.

### 🔄 반응성과 권위성이 조화된 멀티플레이 아키텍처

클라이언트의 예측 보간(Interpolation)과 API 서버의 엄격한 유효성 검증을 결합했습니다. 로컬 스토리지에 진행도를 저장해 빠른 이어하기를 지원하고, 핵심 상태는 서버 틱(Tick) 기준으로 판정하여 네트워크 지연 속에서도 공정한 협동 플레이를 보장합니다.

## 🎬 시연 영상



## 👋 팀원 소개

<table style="text-align: center; width: 100%;">
  <tr>
    <td style="padding: 10px;">
        <a href="https://github.com/ssb2ss"><img src="https://github.com/ssb2ss.png" width="50px"></a><br>
        20213099<br><b>홍성목</b>
    </td>
    <td style="padding: 10px;">
      <a href="https://github.com/kds-02"><img src="https://github.com/kds-02.png" width="50px"></a><br>
        20212962<br><b>김동신</b>
    </td>
    <td style="padding: 10px;">
      <a href="https://github.com/nadiakoz"><img src="https://github.com/nadiakoz.png" width="50px"></a><br>
        20233573<br><b>나디아 코즐로바</b>
    </td>
    <td style="padding: 10px;">
      <a href="https://github.com/zouea4879"><img src="https://github.com/zouea4879.png" width="50px"></a><br>
        20213014<br><b>손승완</b>
    </td>
    <td style="padding: 10px;">
      <a href="https://github.com/chohyeonho"><img src="https://github.com/chohyeonho.png" width="50px"></a><br>
        20213087<br><b>조현호</b>
    </td>
    <td style="padding: 10px;">
      <a href="https://github.com/minho2002"><img src="https://github.com/minho2002.png" width="50px"></a><br>
        20213089<br><b>최민호</b>
    </td>
  </tr>
  
  <tr>
    <td style="height: 50px; padding: 10px;">팀장<br>플레이어 개발</td>
    <td style="padding: 10px;">백엔드 개발</td>
    <td style="padding: 10px;">3D 모델링</td>
    <td style="padding: 10px;">적 개발<br>맵 디자인</td>
    <td style="padding: 10px;">인벤토리 개발</td>
    <td style="padding: 10px;">아트 디렉터<br>UI/UX</td>
  </tr>
</table>

## 🌐 시스템 구조

## 🛠 기술 스택

### 💻 Frontend

| **역할** | **종류** |
|-|-|
| **Engine** | Unity 6.3 LTS |
| **Language** | C# |
| **Multiplayer** | Photon |
| **UI Framework** | HTML, CSS |

### 💻 Backend

| **역할** | **종류** |
|-|-|
| **Framework** |  |
| **Database** |  |
| **ORM** |  |
| **API** |  |

### 💻 Art

| **역할** | **종류** |
|-|-|
| **Sprite Design** |  |
| **3D Modeling & UV** |  |
| **Texturing** |  |

### 💻 Common

| **역할** | **종류** |
|-|-|
| **Version Control** | Git |
| **Project Management** | Notion |

## 🚀 실행 가이드



## 📂 폴더 구조



## 📑 출품 및 퍼블리싱 계획

### 🎪 PlayX4 2024 오프라인 부스 운영

**일정:** 5월 21일 ~ 5월 24일 (4일간), KINTEX

**내용:** 일반 게이머 대상 오프라인 데모 빌드 시연 및 실시간 UX/UI 피드백 수집

**현장 마케팅:** SDI 공식 마스코트 '설이'를 활용한 실물 굿즈(키링, 스티커 등) 제작 및 테스트 참여자 대상 증정

### 🌐 글로벌 플랫폼 퍼블리싱 (STEAM)

**타겟 플랫폼:** PC (Steam)

**출시 목표:** 2026년 하반기 정식 출시 예정

**계획:** PlayX4에서 수집된 유저 피드백을 바탕으로 밸런싱 작업 및 폴리싱을 거친 후 스팀 스토어에 정식 배포