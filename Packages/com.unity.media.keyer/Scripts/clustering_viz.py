import random
import argparse
import plotly
import plotly.graph_objects as go
import numpy as np
import os, sys

# Taken from https://raw.githubusercontent.com/CircusMonkey/covariance-ellipsoid/master/ellipsoid.py
# The original version sorts the eigen values/vectors, which is not needed in our case.
# We use the eigen vectors to deform a sphere.
def get_cov_ellipsoid(cov, mu, count, nstd):
    """
    Return the 3d points representing the covariance matrix
    cov centred at mu and scaled by the factor nstd.
    """
    assert(cov.shape == (3, 3))

    # Find eigenvalues to correspond to the covariance matrix
    eigvals, eigvecs = np.linalg.eigh(cov)

    # Set of all spherical angles to draw our ellipsoid
    theta = np.linspace(0, 2 * np.pi, count)
    phi = np.linspace(0, np.pi, count)

    # Width, height and depth of ellipsoid
    rx, ry, rz = nstd * np.sqrt(eigvals)

    # Get the xyz points for plotting
    # Cartesian coordinates that correspond to the spherical angles:
    X = rx * np.outer(np.cos(theta), np.sin(phi))
    Y = ry * np.outer(np.sin(theta), np.sin(phi))
    Z = rz * np.outer(np.ones_like(theta), np.cos(phi))

    # Rotate ellipsoid for off axis alignment
    # Flatten to vectorise rotation
    X,Y,Z = X.flatten(), Y.flatten(), Z.flatten()
    X,Y,Z = np.matmul(eigvecs, np.array([X,Y,Z]))
   
    # Add in offsets for the mean
    X = X + mu[0]
    Y = Y + mu[1]
    Z = Z + mu[2]
    
    return X,Y,Z

def get_ellipsoid_indices(count):
  '''
  Evaluate ellipsoid geometry triangle indices.
  '''
  s = (count - 1) * (count - 1) * 2
  i, j, k = np.zeros(s, dtype=int), np.zeros(s, dtype=int), np.zeros(s, dtype=int)
  ii = 0
  for y in range(count - 1):
    k1 = y * count
    k2 = (y + 1) * count
    for x in range(count - 1):
      i[ii] = k1
      j[ii] = k2
      k[ii] = k2 + 1
      ii = ii + 1
      i[ii] = k2 + 1
      j[ii] = k1 + 1
      k[ii] = k1
      ii = ii + 1
      k1 = k1 + 1
      k2 = k2 + 1
  return i, j, k

def parse_input_args():
  '''
  Returns the path of the source directory to load data from,
  and the number of steps
  '''
  parser = argparse.ArgumentParser()
  parser.add_argument('-d', '--dir', required=True, type=str, 
    help="Source directory to load data from.")
  parser.add_argument('-s', '--steps', required=True, type=int, 
    help="Number of iteration steps for which centroids and covariances were computed.")
  parser.add_argument('-m', '--maxsamples', nargs='?', default=-1, type=int, 
    help="Maximum number of samples to process.")

  args = parser.parse_args()
  directory = args.dir
  
  if not os.path.isdir(directory):
    print('The specified path does not exist.')
    sys.exit()

  return directory, args.steps, args.maxsamples

def load_data(base_dir, steps):
  '''
  Load serialized data and returns properly shaped 
  (samples, list of centroids, list of covariances).
  '''
  samples_path = os.path.join(base_dir, 'samples.txt')
  samples = np.loadtxt(samples_path, dtype=float).reshape((-1, 4))

  centroids = list()
  covariances = list()

  for i in range(steps + 1):
    centroids_path = os.path.join(base_dir, f'centroids_{i:03d}.txt')
    centroids.append(np.loadtxt(centroids_path, dtype=float).reshape((-1, 3)))

    covariances_path = os.path.join(base_dir, f'covariances_{i:03d}.txt')
    covariances.append(np.loadtxt(covariances_path, dtype=float).reshape((-1, 3, 3)))
  
  # Optional reference cluster data.
  ref_centroids = None
  ref_centroids_path = os.path.join(base_dir, 'ref_centroids.txt')
  if os.path.isfile(ref_centroids_path):
    ref_centroids = np.loadtxt(ref_centroids_path, dtype=float).reshape((-1, 3))

  ref_covariances = None
  ref_covariances_path = os.path.join(base_dir, 'ref_covariances.txt')
  if os.path.isfile(ref_covariances_path):
    ref_covariances = np.loadtxt(ref_covariances_path, dtype=float).reshape((-1, 3, 3))

  return samples, centroids, covariances, ref_centroids, ref_covariances

def cluster_trace_name(i): return f'cluster_{i}'

def cluster_trace_color(centroid): return f'rgb({centroid[0] * 255}, {centroid[1] * 255}, {centroid[2] * 255})'

def make_cluster_traces(centroids, covariances, indices, count):
  assert(centroids.shape[0] == covariances.shape[0])
  # Clusters traces, ellipsoid meshes.
  traces = []
  for i in range(centroids.shape[0]):
    x, y, z = get_cov_ellipsoid(covariances[i], centroids[i], count, 1)    
    traces.append(go.Mesh3d(
      name=cluster_trace_name(i),
      hoverinfo='none',
      x=x, y=y, z=z, 
      i=indices[0], j=indices[1], k=indices[2], 
      color=cluster_trace_color(centroids[i]), 
      opacity=0.25, 
      lightposition=dict(
        x=0.5, 
        y=1, 
        z=0.5),
      lighting=dict(
        ambient=0.9,
        diffuse=0.9,
        roughness=0.1, 
        specular=2)))
  return traces

def make_samples_trace(samples):
  # We perceive point size through their surface not their radius.
  point_size = np.sqrt(samples[:,3] / np.pi)
  point_size = np.clip(point_size * 4, 0, 128)

  # Samples trace, a pointcloud.
  return go.Scatter3d(
      name='pointcloud',
      x=samples[:,0], 
      y=samples[:,1], 
      z=samples[:,2], 
      mode ='markers',
      hoverinfo='none',
      marker = dict(
       line = dict(width=0),
       size = point_size,
       color = samples[:,:3],
       opacity = 0.25))

def make_frame_data(centroids, covariances, count):
  '''
  Evaluate changing data for cluster traces.
  In subsequent frames only the vertices positions change.
  '''
  data = []
  for i in range(centroids.shape[0]):
    x, y, z = get_cov_ellipsoid(covariances[i], centroids[i], count, 1)
    data.append(go.Mesh3d(
      name=cluster_trace_name(i),
      color=cluster_trace_color(centroids[i]), 
      x=x, y=y, z=z))
  return data

def make_frames(centroids, covariances, count, steps):
  '''
  Populate frame data with changing clusters.
  '''
  # we only want to update the cluster traces, not the pointcloud.
  num_clusters = centroids[0].shape[0]
  traces = [i for i in range(1, num_clusters + 1)]
  frames = list()
  for i in range(steps + 1):
    frames.append(go.Frame(
      name=str(i),
      traces=traces,
      data=make_frame_data(centroids[i], covariances[i], count)))
  return frames

def frame_args(duration):
  return {
          "frame": {"duration": duration},
          "mode": "immediate",
          "fromcurrent": True,
          "transition": {"duration": duration, "easing": "linear"}}

def main():
  # TODO Write a .bat file to rerun last execution?
  directory, steps, max_samples = parse_input_args()
  samples, centroids, covariances, ref_centroids, ref_covariances = load_data(directory, steps)

  # We may want to only draw a subset of the samples to not crash the webpage.
  '''if max_samples != -1 and samples.shape[0] > max_samples:
    indices = np.argsort(samples[:, 3])
    samples = samples[indices[::-1]]
    samples = samples[:max_samples]'''

  # Resolution of clusters ellipsoids.
  count = 24
  # Mesh indices only depend on resolution, we reuse them.
  indices = get_ellipsoid_indices(count)
  
  # Plotting data evaluation.
  init_data = []
  init_data.append(make_samples_trace(samples))
  init_data += make_cluster_traces(centroids[0], covariances[0], indices, count)
  if ref_centroids is not None and ref_covariances is not None:
    init_data += make_cluster_traces(ref_centroids, ref_covariances, indices, count)

  frames = make_frames(centroids, covariances, count, steps)

  # Initialize figure.
  fig = go.Figure(
    frames=frames, 
    data=init_data)

  # Timeline to scrub through frames.
  sliders = None
  if frames is not None:
    sliders = [{
      "pad": {"b": 10, "t": 60},
      "len": 0.9,
      "x": 0.1,
      "y": 0,
      "steps": [
          {
              "args": [[f.name], frame_args(0)],
              "label": str(k),
              "method": "animate",
          } 
          for k, f in enumerate(frames)]
    }]

  # Initialize figure layout.
  fig.update_layout(
    title="EM Clustering",
    sliders=sliders,
    scene = dict(
        hovermode=False,
        aspectmode='cube',
        xaxis = dict(title='Red', showspikes=False, nticks=4, range=[0, 1]),
        yaxis = dict(title='Green', showspikes=False, nticks=4, range=[0, 1]),
        zaxis = dict(title='Blue', showspikes=False, nticks=4, range=[0, 1])))

  fig.show()

if __name__ == "__main__":
   main()