"""대화 피드백 독립 모듈. DB·Redis·네트워크를 직접 사용하지 않는다."""

from .service import generate_feedback

__all__ = ["generate_feedback"]
