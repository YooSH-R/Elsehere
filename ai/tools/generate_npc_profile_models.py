"""공통 인격 스키마에서 AI 워커의 Pydantic 모델을 만든다. 스키마를 고친 뒤 실행하고, 만든 파일은 손으로 고치지 않는다.

    ai/.venv/Scripts/python ai/tools/generate_npc_profile_models.py

원본: docs/api/npc-ai-contracts/npc_shared_schemas_v2.json (백엔드와 함께 쓰는 계약)
결과: ai/worker/app/npc_profile_models.py
커밋한 결과가 지금 스키마로 만든 것과 같은지는 ai/worker/tests/test_npc_profile_models.py가 확인한다.
"""

from pathlib import Path

from datamodel_code_generator import DataModelType, Formatter, InputFileType, PythonVersion, generate

ROOT = Path(__file__).resolve().parents[2]
SCHEMA = ROOT / "docs/api/npc-ai-contracts/npc_shared_schemas_v2.json"
OUTPUT = ROOT / "ai/worker/app/npc_profile_models.py"
HEADER = """\
# 자동 생성 파일. 직접 고치지 말고 원본 스키마를 고친 뒤 다시 만든다.
#   원본: docs/api/npc-ai-contracts/npc_shared_schemas_v2.json
#   다시 만들기: python ai/tools/generate_npc_profile_models.py"""


def render() -> str:
    """스키마로 만든 모델 코드. 언제 실행해도 같은 결과가 나오도록 생성 시각은 넣지 않는다."""
    code = generate(
        SCHEMA,
        input_file_type=InputFileType.JsonSchema,
        output_model_type=DataModelType.PydanticV2BaseModel,
        target_python_version=PythonVersion.PY_312,
        skip_root_model=True,  # 스키마 루트는 $defs를 묶어 둔 것뿐이라 모델로 만들지 않는다
        snake_case_field=True,  # 코드에서는 npc_id, JSON에서는 계약 이름 npcId(alias)
        use_type_alias=True,  # 글 목록의 항목을 감싸는 클래스 없이 str로 (values: list[Value], Value는 길이 규칙이 붙은 str)
        use_annotated=True,
        enum_field_as_literal="all",  # formality 같은 정해진 값은 Enum 대신 Literal (schemas.py와 같은 방식)
        use_schema_description=True,  # 스키마의 description을 docstring으로
        use_field_description=True,
        disable_timestamp=True,
        custom_file_header=HEADER,
        formatters=[Formatter.RUFF_FORMAT, Formatter.RUFF_CHECK],
        settings_path=ROOT / "ai/worker",  # 저장소의 Ruff 설정(한 줄 140자 등)으로 정리
    )
    return code.rstrip("\n") + "\n"  # 생성기는 끝 줄바꿈을 빼고 돌려준다 (파일 끝 줄바꿈은 코드 규약)


def main() -> None:
    OUTPUT.write_text(render(), encoding="utf-8", newline="\n")
    print(f"생성: {OUTPUT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
