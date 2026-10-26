package com.elsehere.user.validation;

import jakarta.validation.Constraint;
import jakarta.validation.Payload;
import java.lang.annotation.Documented;
import java.lang.annotation.ElementType;
import java.lang.annotation.Retention;
import java.lang.annotation.RetentionPolicy;
import java.lang.annotation.Target;

/**
 * 닉네임 규칙(AUTH-04). 한글 완성형·영문·숫자만 쓰고, 2자 이상이며 한글은 2, 영문·숫자는 1로 세어 합계 16 이하다.
 *
 * <p>{@code null}은 검사하지 않으므로 필수 값이면 {@code @NotNull}을 함께 붙인다.
 */
@Target({ElementType.FIELD, ElementType.PARAMETER})
@Retention(RetentionPolicy.RUNTIME)
@Documented
@Constraint(validatedBy = NicknameValidator.class)
public @interface Nickname {

  String message() default "닉네임 규칙에 맞지 않습니다.";

  Class<?>[] groups() default {};

  Class<? extends Payload>[] payload() default {};
}
