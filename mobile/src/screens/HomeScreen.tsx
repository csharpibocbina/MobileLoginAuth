import React, { useCallback, useEffect, useState } from 'react';
import { Alert, FlatList, RefreshControl, StyleSheet, Text, View } from 'react-native';
import { authApi } from '../api/authApi';
import { getErrorMessage } from '../api/client';
import type { LoginHistoryItem } from '../api/types';
import { useAuth } from '../auth/AuthContext';
import { PrimaryButton } from '../components/PrimaryButton';
import { colors } from '../components/theme';

const EVENT_LABELS: Record<LoginHistoryItem['eventType'], string> = {
  LoginSucceeded: 'Signed in',
  LoginFailed: 'Failed sign-in',
  Logout: 'Signed out',
};

const EVENT_COLORS: Record<LoginHistoryItem['eventType'], string> = {
  LoginSucceeded: colors.success,
  LoginFailed: colors.danger,
  Logout: colors.muted,
};

export function HomeScreen() {
  const { user, logout, deleteAccount } = useAuth();
  const [history, setHistory] = useState<LoginHistoryItem[]>([]);
  const [refreshing, setRefreshing] = useState(false);
  const [loggingOut, setLoggingOut] = useState(false);

  const loadHistory = useCallback(async () => {
    setRefreshing(true);
    try {
      setHistory(await authApi.loginHistory());
    } catch {
      // History is informational; keep whatever was shown before.
    } finally {
      setRefreshing(false);
    }
  }, []);

  useEffect(() => {
    void loadHistory();
  }, [loadHistory]);

  async function onLogout() {
    setLoggingOut(true);
    try {
      await logout();
    } finally {
      setLoggingOut(false);
    }
  }

  function onDeleteAccount() {
    Alert.alert('Delete account', 'This permanently deletes your account and sign-in history.', [
      { text: 'Cancel', style: 'cancel' },
      {
        text: 'Delete',
        style: 'destructive',
        onPress: async () => {
          try {
            await deleteAccount();
          } catch (err) {
            Alert.alert('Could not delete account', getErrorMessage(err));
          }
        },
      },
    ]);
  }

  return (
    <View style={styles.screen}>
      <View style={styles.header}>
        <Text style={styles.greeting}>Hi, {user?.displayName}</Text>
        <Text style={styles.email}>{user?.email}</Text>
      </View>

      <Text style={styles.sectionTitle}>Recent activity</Text>
      <FlatList
        data={history}
        keyExtractor={(item, index) => `${item.occurredAtUtc}-${index}`}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={loadHistory} />}
        ListEmptyComponent={!refreshing ? <Text style={styles.empty}>No activity yet.</Text> : null}
        renderItem={({ item }) => (
          <View style={styles.row} testID="history-row">
            <View style={[styles.dot, { backgroundColor: EVENT_COLORS[item.eventType] }]} />
            <View style={{ flex: 1 }}>
              <Text style={styles.rowTitle}>{EVENT_LABELS[item.eventType]}</Text>
              <Text style={styles.rowMeta}>
                {new Date(item.occurredAtUtc).toLocaleString()} · {item.deviceInfo ?? 'Unknown device'}
              </Text>
            </View>
          </View>
        )}
      />

      <PrimaryButton title="Log out" onPress={onLogout} loading={loggingOut} />
      <PrimaryButton title="Delete account" variant="secondary" onPress={onDeleteAccount} />
    </View>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, padding: 20, backgroundColor: colors.background },
  header: { backgroundColor: colors.surface, borderRadius: 16, padding: 20, marginBottom: 20 },
  greeting: { fontSize: 24, fontWeight: '700', color: colors.text },
  email: { fontSize: 15, color: colors.muted, marginTop: 4 },
  sectionTitle: { fontSize: 16, fontWeight: '600', color: colors.text, marginBottom: 8 },
  empty: { color: colors.muted, paddingVertical: 12 },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.surface,
    borderRadius: 12,
    padding: 14,
    marginBottom: 8,
  },
  dot: { width: 10, height: 10, borderRadius: 5, marginRight: 12 },
  rowTitle: { fontSize: 15, fontWeight: '600', color: colors.text },
  rowMeta: { fontSize: 13, color: colors.muted, marginTop: 2 },
});
