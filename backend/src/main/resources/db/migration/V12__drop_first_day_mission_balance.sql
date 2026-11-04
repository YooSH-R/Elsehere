-- 퀘스트 코인은 진행에서 계산하므로(Coins) 첫날 미션에 따로 두던 예산은 쓰지 않는다.
alter table first_day_missions drop column balance;
