export interface User {
  id: string;
  email: string;
  displayName: string;
  createdAtUtc: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresAtUtc: string;
  user: User;
}

export interface LoginHistoryItem {
  eventType: 'LoginSucceeded' | 'LoginFailed' | 'Logout';
  ipAddress: string | null;
  deviceInfo: string | null;
  occurredAtUtc: string;
}

export interface NotificationMessage {
  eventId: string;
  type: 'LoginSucceeded' | 'LoginFailed' | 'Logout';
  title: string;
  message: string;
  occurredAtUtc: string;
}
