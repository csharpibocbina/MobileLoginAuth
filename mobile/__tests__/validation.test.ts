import { validateEmail, validatePassword } from '../src/utils/validation';

describe('validateEmail', () => {
  it.each(['', '   ', 'not-an-email', 'a@b'])('rejects %p', (email) => {
    expect(validateEmail(email)).not.toBeNull();
  });

  it('accepts a valid email', () => {
    expect(validateEmail(' jay@example.com ')).toBeNull();
  });
});

// MT-10 (rules shared with RegisterScreen)
describe('validatePassword', () => {
  it.each([
    ['', 'Password is required.'],
    ['Short1', 'Password must be at least 8 characters.'],
    ['ALLUPPER1', 'Password must contain a lowercase letter.'],
    ['alllower1', 'Password must contain an uppercase letter.'],
    ['NoDigitsHere', 'Password must contain a digit.'],
  ])('rejects %p', (password, message) => {
    expect(validatePassword(password)).toBe(message);
  });

  it('accepts a strong password', () => {
    expect(validatePassword('Passw0rd!')).toBeNull();
  });
});
