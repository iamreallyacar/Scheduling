import React from 'react';
import { Link } from 'react-router-dom';

const Landing: React.FC = () => {
  return (
    <div className="min-h-screen bg-gradient-to-br from-indigo-500 via-purple-500 to-pink-500">
      <div className="min-h-screen flex items-center justify-center px-4">
        <div className="max-w-4xl mx-auto text-center text-white">
          <h1 className="text-5xl md:text-7xl font-bold mb-6 bg-gradient-to-r from-white to-gray-200 bg-clip-text text-transparent">
            Welcome to Our App
          </h1>
          <p className="text-xl md:text-2xl mb-8 text-gray-100">
            A modern authentication system with React and .NET
          </p>
          <p className="text-lg mb-12 text-gray-200 max-w-2xl mx-auto">
            Experience secure login and registration with JWT authentication and Google OAuth integration.
            Built with React, TypeScript, and .NET Core.
          </p>
          
          <div className="flex flex-col sm:flex-row gap-4 justify-center">
            <Link
              to="/login"
              className="bg-white text-purple-600 px-8 py-4 rounded-lg font-semibold text-lg hover:bg-gray-100 transform hover:scale-105 transition duration-200 shadow-lg"
            >
              Sign In
            </Link>
            <Link
              to="/register"
              className="bg-transparent border-2 border-white text-white px-8 py-4 rounded-lg font-semibold text-lg hover:bg-white hover:text-purple-600 transform hover:scale-105 transition duration-200"
            >
              Get Started
            </Link>
          </div>

          <div className="mt-16 grid grid-cols-1 md:grid-cols-3 gap-8 max-w-3xl mx-auto">
            <div className="bg-white bg-opacity-10 backdrop-blur-lg rounded-lg p-6">
              <div className="text-3xl mb-4">🔐</div>
              <h3 className="text-xl font-semibold mb-2">Secure Authentication</h3>
              <p className="text-gray-200">JWT-based authentication with secure token management</p>
            </div>
            <div className="bg-white bg-opacity-10 backdrop-blur-lg rounded-lg p-6">
              <div className="text-3xl mb-4">🚀</div>
              <h3 className="text-xl font-semibold mb-2">Modern Tech Stack</h3>
              <p className="text-gray-200">Built with React, TypeScript, and .NET Core</p>
            </div>
            <div className="bg-white bg-opacity-10 backdrop-blur-lg rounded-lg p-6">
              <div className="text-3xl mb-4">📱</div>
              <h3 className="text-xl font-semibold mb-2">Responsive Design</h3>
              <p className="text-gray-200">Beautiful UI that works on all devices</p>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default Landing;
