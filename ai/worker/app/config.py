"""AI 워커 설정. 역할별 모델·공급자 구성은 llm_profiles.json에서 이름 하나로 고른다.

    LLM_PROFILE=gms-luna  → GMS 주소·키(GMS_KEY)·GPT-6 Luna로 (Spring은 그대로)

그 위에 환경 변수로 덮어쓸 수 있다: LLM_BASE_URL·LLM_API_KEY(구성의 기본 공급자), MODEL_<역할>, MODEL_FALLBACK.
"""

import json
import os
from dataclasses import dataclass, field
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]  # 저장소 최상위 (개발할 때 .env를 여기서 읽음)
PROFILES_FILE = Path(os.environ.get("LLM_PROFILES") or Path(__file__).resolve().parents[1] / "llm_profiles.json")
PROFILES = json.loads(PROFILES_FILE.read_text(encoding="utf-8"))
MODEL_PARAMS: list[dict] = PROFILES.get("model_params", [])


def profile_models(name: str) -> tuple[dict[str, str], dict[str, str]]:
    """구성의 (역할 → 모델, 기본 공급자가 아닌 곳을 쓰는 역할 → 공급자 이름)."""
    p = PROFILES["profiles"][name]
    models, role_provider = {}, {}
    for role, m in p["models"].items():
        if isinstance(m, dict):
            if m["provider"] != p["provider"]:
                role_provider[role] = m["provider"]
            m = m["model"]
        models[role] = m
    return models, role_provider


def profile_stt_fallback(name: str) -> str:
    """구성의 음성 인식 대체 '공급자:모델' (models.stt.fallback, 없으면 '')."""
    m = PROFILES["profiles"][name]["models"].get("stt")
    fb = m.get("fallback") if isinstance(m, dict) else None
    return f"{fb['provider']}:{fb['model']}" if fb else ""


def parse_stt_fallback(text: str) -> dict[str, str]:
    """'gms:whisper-1' → {'provider': 'gms', 'model': 'whisper-1'}. 비어 있으면 {} (대체 없음)."""
    if not text.strip():
        return {}
    provider, _, model = text.strip().partition(":")
    if provider not in PROFILES["providers"] or not model:
        raise RuntimeError(f"STT_FALLBACK은 '공급자:모델' (공급자는 {', '.join(PROFILES['providers'])}), 끄려면 빈 값: {text!r}")
    return {"provider": provider, "model": model}


def parse_stt_chain(text: str) -> list[dict[str, str]]:
    """'gpu:turbo-lr1e4,gms:whisper-1' → 앞에서부터 부를 음성 인식 공급자 목록. 비어 있으면 [] (구성의 음성 인식 + STT_FALLBACK)."""
    if not text.strip():
        return []
    chain = []
    for item in text.split(","):
        provider, _, model = item.strip().partition(":")
        if provider not in PROFILES["providers"] or not model:
            raise RuntimeError(f"STT_CHAIN은 '공급자:모델,공급자:모델' (공급자는 {', '.join(PROFILES['providers'])}): {text!r}")
        chain.append({"provider": provider, "model": model})
    return chain


def provider_base_url(name: str, env=os.environ) -> str:
    """공급자 주소. gpu처럼 배포마다 다른 공급자는 json의 base_url_env가 가리키는 환경 변수에서 읽는다 (없으면 '')."""
    p = PROFILES["providers"][name]
    return (env.get(p["base_url_env"], "") if "base_url_env" in p else p["base_url"]).rstrip("/")


# 역할별 기본 모델 (기본 구성 gms-luna). 고른 근거는 llm_profiles.json 각 구성의 _note
DEFAULT_PROFILE = PROFILES["default_profile"]
DEFAULT_MODELS, DEFAULT_ROLE_PROVIDER = profile_models(DEFAULT_PROFILE)
DEFAULT_FALLBACK = ",".join(f"{a}={b}" for a, b in PROFILES["profiles"][DEFAULT_PROFILE].get("fallback", {}).items())
DEFAULT_STT_FALLBACK = parse_stt_fallback(profile_stt_fallback(DEFAULT_PROFILE))


def load_dotenv(path: Path = ROOT / ".env"):
    """개발용: 저장소 루트 .env를 읽어 비어 있는 환경 변수만 채운다 (배포에서는 컨테이너 환경 변수를 씀)."""
    if not path.exists():
        return
    for line in path.read_text(encoding="utf-8-sig").splitlines():
        if "=" in line and not line.lstrip().startswith("#"):
            k, v = line.split("=", 1)
            os.environ.setdefault(k.strip(), v.strip().strip('"').strip("'"))


def parse_pairs(text: str) -> dict[str, str]:
    """'a=b,c=d' → {'a': 'b', 'c': 'd'}"""
    return dict(p.split("=", 1) for p in (x.strip() for x in text.split(",")) if "=" in p)


def provider_key(name: str, env=os.environ) -> str:
    return next((env[k] for k in PROFILES["providers"][name]["key_env"] if env.get(k)), "")


@dataclass
class Settings:
    token: str  # Spring과 공유하는 내부 토큰 (X-Internal-Token)
    base_url: str = PROFILES["providers"][PROFILES["profiles"][DEFAULT_PROFILE]["provider"]]["base_url"]  # 구성의 기본 공급자
    api_key: str = ""
    profile: str = DEFAULT_PROFILE
    models: dict[str, str] = field(default_factory=lambda: dict(DEFAULT_MODELS))
    fallback: dict[str, str] = field(default_factory=lambda: parse_pairs(DEFAULT_FALLBACK))
    role_provider: dict[str, str] = field(
        default_factory=lambda: dict(DEFAULT_ROLE_PROVIDER)
    )  # 역할 → 다른 공급자 이름 (예: GMS 구성에서도 stt는 groq)
    providers: dict[str, dict] = field(default_factory=dict)  # 다른 공급자 이름 → {"base_url", "api_key"}
    stt_fallback: dict[str, str] = field(default_factory=lambda: dict(DEFAULT_STT_FALLBACK))  # {"provider", "model"}, {}면 대체 없음
    stt_first_timeout_s: float = 8.0  # 대체가 있을 때 첫 음성 인식 호출의 상한 (Groq 6.5초 음성 0.4초). 대체·대체 없음은 30초
    stt_chain: list[dict[str, str]] = field(default_factory=list)  # 음성 인식 공급자 순서 [{"provider", "model"}]. 비면 위 두 칸으로
    stt_next_timeout_s: float = 8.0  # 순서 중간 공급자(처음·마지막 아님)의 상한. GMS whisper-1 6.5초 음성 중앙 1.9초
    stt_breaker_failures: int = 3  # 같은 음성 인식 공급자가 이만큼 연속 실패하면 잠시 건너뜀 (0이면 끔). 마지막 공급자는 건너뛰지 않음
    stt_breaker_skip_s: float = 60.0  # 건너뛰는 시간. 지나면 다시 시도 (GPU 서버가 꺼졌을 때 매 턴 첫 상한만큼 기다리지 않게)
    model_params: list[dict] = field(default_factory=lambda: list(MODEL_PARAMS))
    call_timeout_s: float = 8.0  # 모델 호출 하나의 상한. 넘으면 끊고 재시도·대체 모델
    hedge_after_s: float = 3.0  # 한 번에 받는 호출이 이만큼 답이 없으면 같은 요청을 하나 더 보내 먼저 온 답을 씀 (0이면 끔).
    # 2026-10-22 gms-luna: 혼잡한 때 8초 멈춘 호출이 다시 보내면 1~2초, 평소 p99 2.5~3.9초
    stream_hedge_after_s: float = 2.5  # NPC 대답 스트림의 첫 글자가 이만큼 안 오면 같은 요청 스트림을 하나 더 (0이면 끔).
    # gms-luna 첫 글자 40번 p50 0.98s·최대 1.66s, 가끔 11s+ 멈춤. 다시 보낸 쪽이 ~1s라 첫 글자 상한(5s) 안에 든다
    feedback_timeout_s: float = 60.0  # 대화 종료 피드백 호출 하나의 상한 (길게 생성해서 따로, 같은 요청 더 보내기 없음)
    words_budget_s: float = 3.0  # NPC 대답 스트림 안 추천 단어(words)의 상한. 넘으면 words: null(timeout)로 넘기고 done을 보낸다
    judge_budget_s: float = 5.0  # 판정 요청의 의도·무례·번역 각각의 상한 (재시도·대체 모델 포함). 웹 프로토타입 실측 1.2~2.9초
    guard_budget_s: float = 5.0  # /v1/guard 무례 판정 하나의 상한 (로컬 판정기 + LLM 재시도·대체 모델 포함)
    guard_gpu_model: str = ""  # GPU 서버 판정 모델(gpu_llm 공급자). 있으면 먼저 부르고 실패하면 구성의 guard 모델. 비면 끔
    guard_gpu_timeout_s: float = 2.0  # GPU 서버 판정 한 번의 상한 (실측 p90 0.3초). 넘으면 바로 구성의 guard 모델로
    guard_breaker_failures: int = 3  # GPU 서버 판정이 이만큼 연속 실패하면 잠시 건너뜀 (0이면 끔)
    guard_breaker_skip_s: float = 60.0  # 건너뛰는 시간. 지나면 다시 시도
    reply_first_budget_s: float = 5.0  # NPC 대답 첫 글자까지의 상한 (재시도·대체 모델 포함)
    respond_budget_s: float = 30.0  # NPC 추론 API(/internal/ai/npc/respond) 요청 하나의 상한. 계약 10절 'AI 처리 제한 30초'
    exhausted_skip_s: float = 600.0  # 하루 한도에 걸린 모델은 이만큼 건너뜀
    retry_pause_s: float = 1.0  # 429/5xx 뒤 재시도 전에 쉬는 시간 (retry-after가 있으면 그것, 최대 5초)
    kokoro_model: str = ""  # 비어 있으면 음성 합성 없음 (tts: false로 알림)
    kokoro_voices: str = ""
    tts_require_timing: bool = False
    # 합성 스레드 수. 0이면 onnxruntime 기본(보이는 코어 수). 컨테이너 CPU 상한보다 많으면 할당량에 걸려 몇 배 느려진다
    # (2026-10-29 i7-13700H Docker 2 CPU 상한: 기본 20스레드 대신 2스레드로 6초 소리 문장 합성 약 3초)
    kokoro_threads: int = 0
    tts_chunk_budget_s: float = 2.5  # NPC 대답 한 조각의 합성이 이보다 늦으면 tts: false (턴 전체 음성에는 기다려 넣음)
    audio_max_mb: float = 200.0  # 음성 조각·턴 전체 음성 보관소의 전체 크기 상한 (넘으면 오래된 것부터 버림)
    audio_ttl_s: float = 600.0  # 스트림 음성 조각 보관 시간 (GET /v1/audio/{id})
    guard_local_model: str = ""  # 로컬 무례 판정기 폴더. 비어 있으면 끔 (기본, 판정은 전부 LLM) — app/guard_local.py
    guard_t_lo: float = 0.0  # 로컬 점수 ≤ t_lo → 아님, ≥ t_hi → 무례, 그 사이는 LLM
    guard_t_hi: float = 1.0  # (모델을 켜면 둘 다 반드시 정함. 학습에 쓰지 않은 보정용 데이터에서 고른 값)

    @classmethod
    def from_env(cls) -> "Settings":
        load_dotenv()
        e = os.environ
        token = e.get("AI_WORKER_TOKEN", "")
        if not token:
            raise RuntimeError("AI_WORKER_TOKEN이 없습니다. Spring과 같은 값을 환경 변수(또는 저장소 루트 .env)에 넣으세요.")
        profile = e.get("LLM_PROFILE") or DEFAULT_PROFILE
        if profile not in PROFILES["profiles"]:
            raise RuntimeError(f"없는 LLM_PROFILE {profile} (있는 것: {', '.join(PROFILES['profiles'])}, {PROFILES_FILE.name})")
        p = PROFILES["profiles"][profile]
        main = p["provider"]
        models, role_provider = profile_models(profile)
        api_key = e.get("LLM_API_KEY") or provider_key(main, e)
        stt_fallback = parse_stt_fallback(e.get("STT_FALLBACK", profile_stt_fallback(profile)))
        stt_chain = parse_stt_chain(e.get("STT_CHAIN", ""))
        others = list(role_provider.values())
        if stt_fallback and stt_fallback["provider"] != main:
            others.append(stt_fallback["provider"])
        others += [step["provider"] for step in stt_chain if step["provider"] != main]
        guard_gpu_model = e.get("GUARD_GPU_MODEL", "")
        if guard_gpu_model:  # 주소·키가 없으면 아래 missing에 걸린다
            others.append("gpu_llm")
        providers = {name: {"base_url": provider_base_url(name, e), "api_key": provider_key(name, e)} for name in dict.fromkeys(others)}
        missing = [PROFILES["providers"][n]["base_url_env"] for n, v in providers.items() if not v["base_url"]]
        missing += [" 또는 ".join(PROFILES["providers"][n]["key_env"]) for n, v in providers.items() if not v["api_key"]]
        if not api_key:
            missing.insert(0, " 또는 ".join(["LLM_API_KEY", *PROFILES["providers"][main]["key_env"]]))
        if missing:
            raise RuntimeError(f"구성 {profile}에 필요한 모델 API 키가 없습니다: {', '.join(missing)} (환경 변수 또는 저장소 루트 .env)")
        default_fallback = ",".join(f"{a}={b}" for a, b in p.get("fallback", {}).items())
        guard_model = e.get("GUARD_LOCAL_MODEL", "")
        t_lo, t_hi = cls.guard_t_lo, cls.guard_t_hi
        if guard_model:  # 기준값을 기본값으로 두면 조용히 아무것도 안 하므로, 켤 때는 반드시 정하게 한다
            if not (e.get("GUARD_LOCAL_T_LO") and e.get("GUARD_LOCAL_T_HI")):
                raise RuntimeError("GUARD_LOCAL_MODEL을 켜면 GUARD_LOCAL_T_LO·GUARD_LOCAL_T_HI도 정해야 합니다 (예: 0.05, 0.95)")
            t_lo, t_hi = float(e["GUARD_LOCAL_T_LO"]), float(e["GUARD_LOCAL_T_HI"])
            if not 0.0 <= t_lo < t_hi <= 1.0:
                raise RuntimeError(f"무례 판정 기준값은 0 ≤ T_LO < T_HI ≤ 1 이어야 합니다 (지금 {t_lo}, {t_hi})")
        return cls(
            token=token,
            base_url=e.get("LLM_BASE_URL", PROFILES["providers"][main]["base_url"]).rstrip("/"),
            api_key=api_key,
            profile=profile,
            models={role: e.get(f"MODEL_{role.upper()}", m) for role, m in models.items()},
            fallback=parse_pairs(e.get("MODEL_FALLBACK", default_fallback)),
            role_provider=role_provider,
            providers=providers,
            stt_fallback=stt_fallback,
            stt_first_timeout_s=float(e.get("STT_FIRST_TIMEOUT_S", cls.stt_first_timeout_s)),
            stt_chain=stt_chain,
            stt_next_timeout_s=float(e.get("STT_NEXT_TIMEOUT_S", cls.stt_next_timeout_s)),
            stt_breaker_failures=int(e.get("STT_BREAKER_FAILURES", cls.stt_breaker_failures)),
            stt_breaker_skip_s=float(e.get("STT_BREAKER_SKIP_S", cls.stt_breaker_skip_s)),
            call_timeout_s=float(e.get("CALL_TIMEOUT_S", cls.call_timeout_s)),
            hedge_after_s=float(e.get("HEDGE_AFTER_S", cls.hedge_after_s)),
            stream_hedge_after_s=float(e.get("STREAM_HEDGE_AFTER_S", cls.stream_hedge_after_s)),
            feedback_timeout_s=float(e.get("FEEDBACK_TIMEOUT_S", cls.feedback_timeout_s)),
            words_budget_s=float(e.get("WORDS_BUDGET_S", cls.words_budget_s)),
            judge_budget_s=float(e.get("JUDGE_BUDGET_S", cls.judge_budget_s)),
            guard_budget_s=float(e.get("GUARD_BUDGET_S", cls.guard_budget_s)),
            guard_gpu_model=guard_gpu_model,
            guard_gpu_timeout_s=float(e.get("GUARD_GPU_TIMEOUT_S", cls.guard_gpu_timeout_s)),
            guard_breaker_failures=int(e.get("GUARD_BREAKER_FAILURES", cls.guard_breaker_failures)),
            guard_breaker_skip_s=float(e.get("GUARD_BREAKER_SKIP_S", cls.guard_breaker_skip_s)),
            audio_max_mb=float(e.get("AUDIO_MAX_MB", cls.audio_max_mb)),
            reply_first_budget_s=float(e.get("REPLY_FIRST_BUDGET_S", cls.reply_first_budget_s)),
            respond_budget_s=float(e.get("RESPOND_BUDGET_S", cls.respond_budget_s)),
            kokoro_model=e.get("KOKORO_MODEL", ""),
            kokoro_voices=e.get("KOKORO_VOICES", ""),
            tts_require_timing=e.get("TTS_REQUIRE_TIMING", "false").lower() == "true",
            kokoro_threads=int(e.get("KOKORO_THREADS", cls.kokoro_threads)),
            tts_chunk_budget_s=float(e.get("TTS_CHUNK_BUDGET_S", cls.tts_chunk_budget_s)),
            guard_local_model=guard_model,
            guard_t_lo=t_lo,
            guard_t_hi=t_hi,
        )
