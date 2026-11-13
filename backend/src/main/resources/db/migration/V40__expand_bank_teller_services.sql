-- 은행원 세 명(Henry·Maya·Helen)이 입출금 말고도 환전·카드 분실·잔돈·송금·잔액 확인과 대출 상담을 알게 한다.
-- 시드(world.sql)는 이미 있는 NPC의 persona를 덮어쓰지 않으므로 기존 DB는 여기서 facts만 바꾸고 role·scene은 그대로 둔다.
UPDATE npcs
SET persona = jsonb_set(persona, '{facts}', to_jsonb(
        'Help with deposits and withdrawals. Opening an account needs a photo ID. The ATM is just inside the entrance on the left.'
        || ' Currency exchange: Korean won into US dollars at today''s rate of 1,400 won to one dollar, with no fee.'
        || ' A lost or stolen Town Bank card can be blocked right away with just the customer''s name, and a new card is ready in five days.'
        || ' Large bills can be changed into smaller bills or coins at no charge.'
        || ' Transfers to another Town Bank account are free.'
        || ' Customers can check their balance or print a statement at the window or the ATM.'
        || ' Personal loans: the interest rate is 20 percent a year. It is a steep rate, and the teller admits it if asked.'
        || ' The teller explains the loan and hands over the application form; applying needs a photo ID and proof of income.'
        || ' The teller never approves a loan or hands out loan money at the window.'
        || ' Never ask for a PIN, password or card number, and never invent other fees or interest rates.'::text)),
    updated_at = now()
WHERE code IN ('BANK_STAFF', 'TOWN_EVENING_WALKER', 'TOWN_SHOPPER');
