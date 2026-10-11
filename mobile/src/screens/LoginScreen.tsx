import { useRouter } from 'expo-router';
import React, { useState } from 'react';
import { KeyboardAvoidingView, Platform, StyleSheet, Text, View } from 'react-native';
import { getErrorMessage } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import { FormField } from '../components/FormField';
import { PrimaryButton } from '../components/PrimaryButton';
import { colors } from '../components/theme';
import { validateEmail } from '../utils/validation';

export function LoginScreen() {
  const { login } = useAuth();
  const router = useRouter();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<{ email?: string | null; password?: string | null }>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function onSubmit() {
    const next = { email: validateEmail(email), password: password ? null : 'Password is required.' };
    setErrors(next);
    setFormError(null);
    if (next.email || next.password) return;

    setLoading(true);
    try {
      await login(email, password);
    } catch (err) {
      setFormError(getErrorMessage(err, 'Invalid email or password.'));
    } finally {
      setLoading(false);
    }
  }

  return (
    <KeyboardAvoidingView style={styles.screen} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <View style={styles.card}>
        <Text style={styles.title}>Welcome back</Text>
        <Text style={styles.subtitle}>Sign in to your account</Text>

        <FormField
          label="Email"
          value={email}
          onChangeText={setEmail}
          error={errors.email}
          autoCapitalize="none"
          autoComplete="email"
          keyboardType="email-address"
          textContentType="emailAddress"
          placeholder="you@example.com"
        />
        <FormField
          label="Password"
          value={password}
          onChangeText={setPassword}
          error={errors.password}
          secureTextEntry
          autoComplete="password"
          textContentType="password"
          placeholder="••••••••"
        />

        {formError ? (
          <Text accessibilityRole="alert" style={styles.formError}>
            {formError}
          </Text>
        ) : null}

        <PrimaryButton title="Log in" onPress={onSubmit} loading={loading} />
        <PrimaryButton title="Create an account" variant="secondary" onPress={() => router.push('/register')} />
      </View>
    </KeyboardAvoidingView>
  );
}

export const authScreenStyles = StyleSheet.create({
  screen: { flex: 1, justifyContent: 'center', padding: 20, backgroundColor: colors.background },
  card: { backgroundColor: colors.surface, borderRadius: 16, padding: 24 },
  title: { fontSize: 26, fontWeight: '700', color: colors.text },
  subtitle: { fontSize: 15, color: colors.muted, marginTop: 4, marginBottom: 24 },
  formError: { color: colors.danger, marginBottom: 8, fontSize: 14 },
});

const styles = authScreenStyles;
