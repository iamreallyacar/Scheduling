import React, { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { apiService } from '../services/api';

const OAuthSuccess: React.FC = () => {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const [message, setMessage] = useState('Processing login...');

  useEffect(() => {
    const processOAuthSuccess = async () => {
      const token = searchParams.get('token');
      const error = searchParams.get('error');

      if (error) {
        setMessage(`Login failed: ${decodeURIComponent(error)}`);
        setTimeout(() => {
          navigate('/login');
        }, 3000);
        return;
      }

      if (token) {
        try {
          // Store the token
          localStorage.setItem('token', token);
          setMessage('Login successful! Getting your profile...');
          
          // Get user profile to store user data
          const response = await apiService.getProfile();
          localStorage.setItem('user', JSON.stringify(response.user));
          
          setMessage('Login successful! Redirecting to dashboard...');
          
          // Force a page reload to refresh the auth context
          setTimeout(() => {
            window.location.href = '/dashboard';
          }, 1000);
        } catch (error) {
          console.error('Failed to get user profile:', error);
          setMessage('Login successful but failed to get profile. Redirecting...');
          setTimeout(() => {
            window.location.href = '/dashboard';
          }, 2000);
        }
      } else {
        setMessage('No authentication token received. Redirecting to login...');
        setTimeout(() => {
          navigate('/login');
        }, 2000);
      }
    };

    processOAuthSuccess();
  }, [searchParams, navigate]);
  return (
    <div className="min-h-screen bg-gradient-to-br from-green-500 to-blue-600 flex items-center justify-center px-4">
      <div className="max-w-md w-full bg-white rounded-xl shadow-2xl p-8 text-center">
        <div className="mb-6">
          {message.includes('successful') ? (
            <div className="w-16 h-16 bg-green-100 rounded-full flex items-center justify-center mx-auto">
              <svg className="w-8 h-8 text-green-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
              </svg>
            </div>
          ) : message.includes('failed') ? (
            <div className="w-16 h-16 bg-red-100 rounded-full flex items-center justify-center mx-auto">
              <svg className="w-8 h-8 text-red-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
              </svg>
            </div>
          ) : (
            <div className="animate-spin rounded-full h-16 w-16 border-b-2 border-blue-500 mx-auto"></div>
          )}
        </div>
        <h1 className="text-2xl font-bold text-gray-800 mb-4">Google OAuth</h1>
        <p className="text-gray-600 mb-4">{message}</p>
        
        {/* Debug information */}
        <div className="text-xs text-gray-400 mt-6 text-left bg-gray-50 p-3 rounded">
          <div>Token: {searchParams.get('token') ? 'Present' : 'Missing'}</div>
          <div>Error: {searchParams.get('error') || 'None'}</div>
          <div>Stored User: {localStorage.getItem('user') ? 'Yes' : 'No'}</div>
          <div>Stored Token: {localStorage.getItem('token') ? 'Yes' : 'No'}</div>
        </div>
      </div>
    </div>
  );
};

export default OAuthSuccess;
