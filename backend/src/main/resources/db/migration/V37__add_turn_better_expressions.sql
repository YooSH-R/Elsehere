-- 틀리진 않았지만 어색하거나 짧은 영어에 보여 줄 같은 뜻의 더 좋은 문장(JSON 문자열 배열). 없으면 null
alter table turns add column better_expressions jsonb;
