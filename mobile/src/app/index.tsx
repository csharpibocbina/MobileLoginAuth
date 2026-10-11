import { Redirect } from 'expo-router';
import { useAuth } from '../auth/AuthContext';

export default function Index() {
  const { status } = useAuth();
  return <Redirect href={status === 'signedIn' ? '/home' : '/login'} />;
}
