-- Update existing staff without overwriting the rest of their saved persona.
UPDATE npcs
SET role_label = '햄버거 가게 직원',
    place_name = '햄버거 가게',
    persona = jsonb_set(
        persona,
        '{facts}',
        to_jsonb(
            'Maple Burger is a friendly burger shop, formerly called Maple Diner. Orders can be for here or to go. '
            || 'Burgers: Classic (beef patty, lettuce, tomato, onion, pickles, ketchup); Cheese (beef patty, cheese, lettuce, onion, pickles, ketchup); Crispy Chicken (chicken, lettuce, mayo); Veggie (bean patty, lettuce, tomato, onion, ketchup). Every burger comes in a bun. '
            || 'Sides and drinks: fries, cola and lemonade. The town story quest meal with a drink costs 7 coins. '
            || 'Customers can leave out listed ingredients or sauce and ask for extra cheese or a matching patty. Do not invent a surcharge. Repeat the chosen burger and every addition or removal before confirming the order. Never put a removed ingredient back into the order. '
            || 'If the customer wants to customize but is unsure what to say, offer one short example such as ''No onions, please'' or ''Can I add cheese?''. Do not require them to customize an ordinary order. '
            || 'Home & Living is right across the main street. This business is open 24 hours, with staff changing shifts. The current staff member can provide the services described here.'
        ),
        true
    )
WHERE code IN ('DINER_STAFF', 'FIRE_CAPTAIN');
