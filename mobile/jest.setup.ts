// In-memory replacement for the Keychain/Keystore so token storage can be asserted in tests.
jest.mock('expo-secure-store', () => {
  const store = new Map<string, string>();
  return {
    __store: store,
    getItemAsync: jest.fn(async (key: string) => store.get(key) ?? null),
    setItemAsync: jest.fn(async (key: string, value: string) => {
      store.set(key, value);
    }),
    deleteItemAsync: jest.fn(async (key: string) => {
      store.delete(key);
    }),
  };
});

jest.mock('expo-device', () => ({ modelName: 'iPhone 15', osName: 'iOS', osVersion: '18.1' }));

jest.mock('react-native-toast-message', () => ({
  __esModule: true,
  default: Object.assign(() => null, { show: jest.fn(), hide: jest.fn() }),
}));

beforeEach(() => {
  // eslint-disable-next-line @typescript-eslint/no-require-imports
  require('expo-secure-store').__store.clear();
});
