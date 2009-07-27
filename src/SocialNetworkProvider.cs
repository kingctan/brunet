/*
Copyright (C) 2009 Pierre St Juste <ptony82@ufl.edu>, University of Florida

This program is free software; you can redistribute it and/or
modify it under the terms of the GNU General Public License
as published by the Free Software Foundation; either version 2
of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program; if not, write to the Free Software
Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using Brunet;
using Brunet.DistributedServices;

#if SVPN_NUNIT
using NUnit.Framework;
#endif

namespace SocialVPN {

  /**
   * This class manages all of the social networks and identity providers.
   * Additional networks are registers in the register backends method.
   */
  public class SocialNetworkProvider : IProvider, ISocialNetwork {

    /**
     * The local SocialUser.
     */
    protected readonly SocialUser _local_user;

    /**
     * The byte array for the local certificate.
     */
    protected readonly byte[] _local_cert_data;

    /**
     * Dht object used to store data in P2P data store.
     */
    protected readonly IDht _dht;

    /**
     * The list of identity providers.
     */
    protected readonly Dictionary<string, IProvider> _providers;

    /**
     * The list of social networks.
     */
    protected readonly Dictionary<string, ISocialNetwork> _networks;

    /**
     * List of friends manually added.
     */
    protected readonly List<string> _friends;

    /**
     * List of fingerprints manually added.
     */
    protected readonly List<string> _fingerprints;

    /**
     * List of certificates manually added.
     */
    protected readonly List<byte[]> _certificates;

    /**
     * Constructor.
     * @param dht the dht object.
     * @param user the local user object.
     * @param certData the local certificate data.
     */
    public SocialNetworkProvider(IDht dht, SocialUser user, byte[] certData) {
      _local_user = user;
      _dht = dht;
      _providers = new Dictionary<string, IProvider>();
      _networks = new Dictionary<string,ISocialNetwork>();
      _local_cert_data = certData;
      _friends = new List<string>();
      _friends.Add(_local_user.Uid);
      _fingerprints = new List<string>();
      _certificates = new List<byte[]>();
      RegisterBackends();
    }

    /**
     * Registers the various socialvpn backends.
     */
    public void RegisterBackends() {
      /*
      TestNetwork google_backend = new TestNetwork(_local_user,
                                                   _local_cert_data);
      // Registers the identity provider
      _providers.Add("GoogleBackend", google_backend);
      // Register the social network
      _networks.Add("GoogleBackend", google_backend);
      */
    }

    /**
     * Login method to sign in a particular backend.
     * @param id the identifier for the backend.
     * @param the username for the backend.
     * @param the password for the backend.
     * @return a boolean indicating success or failure.
     */
    public bool Login(string id, string username, string password) {
      bool provider_login = true;
      bool network_login = true;
      if(_providers.ContainsKey(id)) {
        provider_login = _providers[id].Login(id, username, password);
      }
      if(_networks.ContainsKey(id)) {
        network_login = _networks[id].Login(id, username, password);
      }
      return (provider_login && network_login);
    }

    /**
     * Get a list of friends from the various backends.
     * @return a list of friends uids.
     */
    public List<string> GetFriends() {
      ProtocolLog.WriteIf(SocialLog.SVPNLog, 
                          String.Format("GET FRIENDS: {0}",
                          DateTime.Now.TimeOfDay));

      List<string> friends = new List<string>();
      foreach(ISocialNetwork network in _networks.Values) {
        List<string> tmp_friends = network.GetFriends();
        if(tmp_friends == null) {
          continue;
        }
        foreach(string friend in tmp_friends) {
          if(friend.Length > 5 || !friends.Contains(friend)) {
            friends.Add(friend);
          }
        }
      }

      // Add friends from manual input
      foreach(string friend in _friends) {
        if(!friends.Contains(friend)) {
          friends.Add(friend);
        }
      }
      return friends;
    }

    /**
     * Get a list of fingerprints from backends.
     * @param uids a list of friend's uids.
     * @return a list of fingerprints.
     */
    public List<string> GetFingerprints(string[] uids) {
      ProtocolLog.WriteIf(SocialLog.SVPNLog, 
                          String.Format("GET FINGERPRINTS: {0}",
                          DateTime.Now.TimeOfDay));

      List<string> fingerprints = new List<string>();
      foreach(IProvider provider in _providers.Values) {
        List<string> tmp_fprs = provider.GetFingerprints(uids);
        if(tmp_fprs == null) {
          continue;
        }
        foreach(string fpr in tmp_fprs) {
          if(fpr.Length >= 45 || !fingerprints.Contains(fpr)) {
            fingerprints.Add(fpr);
          }
        }
      }
      // Add fingerprints from manual input
      foreach(string fpr in _fingerprints) {
        if(!fingerprints.Contains(fpr)) {
          fingerprints.Add(fpr);
        }
      }

      // Get friends from DHT
      foreach(string friend in _friends) {
        byte[] key_bytes = Encoding.UTF8.GetBytes(friend);
        MemBlock keyb = MemBlock.Reference(key_bytes);
        Hashtable[] results = _dht.Get(keyb);
        foreach(Hashtable result in results) {
          byte[] fpr_data = (byte[]) result["value"];
          string fpr = Encoding.UTF8.GetString(fpr_data);
          if(!fingerprints.Contains(fpr)) {
            fingerprints.Add(fpr);
          }
        }
        
      }
      return fingerprints;
    }

    /**
     * Get a list of certificates.
     * @param uids a list of friend's uids.
     * @return a list of certificates.
     */
    public List<byte[]> GetCertificates(string[] uids) {
      ProtocolLog.WriteIf(SocialLog.SVPNLog, 
                          String.Format("GET CERTIFICATES: {0}",
                          DateTime.Now.TimeOfDay));

      List<byte[]> certificates = new List<byte[]>();
      foreach(IProvider provider in _providers.Values) {
        List<byte[]> tmp_certs = provider.GetCertificates(uids);
        if(tmp_certs == null) {
          continue;
        }
        foreach(byte[] cert in tmp_certs) {
          if(!certificates.Contains(cert)) {
            certificates.Add(cert);
          }
        }
      }
      // Add certificates from manual input
      foreach(byte[] cert in _certificates) {
        if(!certificates.Contains(cert)) {
          certificates.Add(cert);
        }
      }
      return certificates;
    }

    /**
     * Stores the fingerprint of local user.
     * @return boolean indicating success.
     */
    public bool StoreFingerprint() {
      ProtocolLog.WriteIf(SocialLog.SVPNLog, 
                          String.Format("STORE FINGERPRINT: {0} {1} {2}",
                          DateTime.Now.TimeOfDay,_local_user.DhtKey,
                          _local_user.Address));

      bool success = false;
      foreach(IProvider provider in _providers.Values) {
        success = (success || provider.StoreFingerprint());
      }
      // Store in the DHT by default
      byte[] key_bytes = Encoding.UTF8.GetBytes(_local_user.Uid);
      MemBlock keyb = MemBlock.Reference(key_bytes);
      byte[] value_bytes = Encoding.UTF8.GetBytes(_local_user.DhtKey);
      MemBlock valueb = MemBlock.Reference(value_bytes);
      if(_dht.Put(keyb, valueb, SocialNode.DHTTTL)) {
        ProtocolLog.WriteIf(SocialLog.SVPNLog, 
                          String.Format("STORE UID SUCCESS: {0} {1} {2}",
                          DateTime.Now.TimeOfDay,_local_user.DhtKey,
                          _local_user.Uid));

      }
      else {
        ProtocolLog.WriteIf(SocialLog.SVPNLog, 
                          String.Format("STORE UID FAILURE: {0} {1} {2}",
                          DateTime.Now.TimeOfDay,_local_user.DhtKey,
                          _local_user.Uid));
      }
      
      return success;
    }

    /**
     * Validates a certificate
     * @param certData the certificate data.
     * @return boolean indicating success.
     */
    public bool ValidateCertificate(byte[] certData) {
      foreach(IProvider provider in _providers.Values) {
        if(provider.ValidateCertificate(certData)) {
          return true;
        }
      }
      // TODO - Statement below should be false
      return true;
    }

    /**
     * Adds a list of fingerprints.
     * @param fprs a list of fingerprints.
     */
    public void AddFingerprints(string[] fprs) {
      foreach(string fpr in fprs) {
        if(!_fingerprints.Contains(fpr)) {
          _fingerprints.Add(fpr);
        }
      }
    }

    /**
     * Adds a certificate to the socialvpn system.
     * @param certString a base64 encoding string representing certificate.
     */
    public void AddCertificate(string certString) {
      certString = certString.Replace("\n", "");
      byte[] certData = Convert.FromBase64String(certString);
      if(!_certificates.Contains(certData)) {
        _certificates.Add(certData);
      }
    }

    /**
     * Adds a list of friends.
     * @param friends a list of friends unique identifiers.
     */
    public void AddFriends(string[] friends) {
      foreach(string friend in friends) {
        if(!_friends.Contains(friend)) {
          _friends.Add(friend);
        }
      }
    }

    /**
     * Remove a list of friends.
     * @param fprs a list of friends unique identifiers.
     */
    public void DeleteFingerprints(string[] fprs) {
      foreach(string fpr in fprs) {
        if(_fingerprints.Contains(fpr)) {
          _fingerprints.Remove(fpr);
        }
      }
    }

  }
#if SVPN_NUNIT
  [TestFixture]
  public class SocialNetworkProviderTester {
    [Test]
    public void SocialNetworkProviderTest() {
      Assert.AreEqual("test", "test");
    }
  } 
#endif
}
